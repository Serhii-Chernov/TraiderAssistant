using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services;

public sealed class WalkForwardStrategyService
{
    internal const string SelectorVersion = "wf-risk-v2-annualized";
    private const int MinimumOuterTrades = 60;
    private const decimal MinimumProfitFactor = 1.15m;
    private const decimal MinimumSharpe = 0.5m;
    private const decimal MaximumDrawdown = 25m;
    private const decimal MaximumProfitConcentration = 50m;
    private static readonly string[] DefaultAssets = { "ETHUSDT", "SOLUSDT" };

    private static readonly string[] DefaultTimeframes =
    {
        "OneDay", "FourHour", "OneHour", "ThirtyMinutes", "FifteenMinutes", "FiveMinutes", "OneMinute"
    };

    private readonly BacktestService _backtestService;
    private readonly MachineLearningService _machineLearningService;
    private readonly AppDbContext _dbContext;
    private readonly StrategyProviderService _strategyProvider;
    private readonly ILogger<WalkForwardStrategyService> _logger;

    public WalkForwardStrategyService(
        BacktestService backtestService,
        MachineLearningService machineLearningService,
        AppDbContext dbContext,
        StrategyProviderService strategyProvider,
        ILogger<WalkForwardStrategyService> logger)
    {
        _backtestService = backtestService;
        _machineLearningService = machineLearningService;
        _dbContext = dbContext;
        _strategyProvider = strategyProvider;
        _logger = logger;
    }

    public async Task<WalkForwardAnalysisReport> RunAsync(
        DateTime startDate,
        DateTime endDate,
        decimal initialDeposit,
        int baseCandidatesPerStrategy = 200,
        int searchCandidatesPerFold = 10000,
        bool activate = false,
        string assetFilter = null,
        string timeframeFilter = null)
    {
        if (endDate <= startDate)
            throw new ArgumentException("End date must be greater than start date.", nameof(endDate));
        if (initialDeposit <= 0m)
            throw new ArgumentOutOfRangeException(nameof(initialDeposit));
        if (baseCandidatesPerStrategy < 100 || baseCandidatesPerStrategy > 1000)
            throw new ArgumentOutOfRangeException(nameof(baseCandidatesPerStrategy),
                "Base candidate count must be between 100 and 1000.");
        if (searchCandidatesPerFold < baseCandidatesPerStrategy || searchCandidatesPerFold > 100000)
            throw new ArgumentOutOfRangeException(nameof(searchCandidatesPerFold));

        startDate = EnsureUtc(startDate);
        endDate = EnsureUtc(endDate);
        var periods = CreatePeriods(startDate, endDate);
        if (periods.Count < 3)
            throw new ArgumentException("Walk-forward analysis requires at least three calendar periods.");

        var groups = CreateGroups(assetFilter, timeframeFilter);
        assetFilter = groups.Select(group => group.Asset).Distinct().Count() == 1 ? groups[0].Asset : null;
        timeframeFilter = groups.Select(group => group.Timeframe).Distinct().Count() == 1
            ? groups[0].Timeframe
            : null;

        var report = new WalkForwardAnalysisReport
        {
            StartDate = startDate,
            EndDate = endDate,
            InitialDeposit = initialDeposit,
            BaseCandidatesPerStrategy = baseCandidatesPerStrategy,
            SearchCandidatesPerFold = searchCandidatesPerFold,
            CandidateSetVersion = StrategyCandidateGenerator.CandidateSetVersion,
            SelectorVersion = SelectorVersion,
            AssetFilter = assetFilter,
            TimeframeFilter = timeframeFilter,
            StrategiesActivated = activate
        };

        foreach (var group in groups)
        {
            _logger.LogInformation(
                "Walk-forward analysis started. Asset: {Asset}, Timeframe: {Timeframe}",
                group.Asset,
                group.Timeframe);
            var result = await AnalyzeGroupAsync(
                group.Asset,
                group.Timeframe,
                periods,
                initialDeposit,
                baseCandidatesPerStrategy,
                searchCandidatesPerFold);
            report.Strategies.Add(result);
            _logger.LogInformation(
                "Walk-forward analysis completed. Asset: {Asset}, Timeframe: {Timeframe}, Accepted: {Accepted}, MlScore: {MlScore}, BaselineScore: {BaselineScore}, MlOutperformedBaseline: {MlOutperformedBaseline}",
                result.Asset,
                result.Timeframe,
                result.IsAccepted,
                result.WalkForwardScore,
                result.Baseline.WalkForwardScore,
                result.Baseline.MlOutperformedBaseline);
        }

        report.Strategies = report.Strategies
            .OrderByDescending(strategy => strategy.IsAccepted)
            .ThenByDescending(strategy => strategy.WalkForwardScore)
            .ThenBy(strategy => strategy.Asset)
            .ThenBy(strategy => strategy.Timeframe)
            .ToList();

        if (activate)
            await ActivateAsync(report);

        _logger.LogInformation(
            "Walk-forward report completed. Strategies: {StrategyCount}, Accepted: {AcceptedCount}, Activated: {ActivatedCount}",
            report.Strategies.Count,
            report.Strategies.Count(strategy => strategy.IsAccepted),
            report.ActiveStrategiesCreated);

        return report;
    }

    internal static List<(string Asset, string Timeframe)> CreateGroups(
        string assetFilter,
        string timeframeFilter)
    {
        string canonicalAsset = CanonicalFilter(assetFilter, DefaultAssets, "asset");
        string canonicalTimeframe = CanonicalFilter(timeframeFilter, DefaultTimeframes, "timeframe");

        return DefaultAssets
            .Where(asset => canonicalAsset == null || asset == canonicalAsset)
            .SelectMany(asset => DefaultTimeframes
                .Where(timeframe => canonicalTimeframe == null || timeframe == canonicalTimeframe)
                .Select(timeframe => (Asset: asset, Timeframe: timeframe)))
            .ToList();
    }

    private async Task<WalkForwardStrategyResult> AnalyzeGroupAsync(
        string asset,
        string timeframe,
        IReadOnlyList<WalkForwardPeriod> periods,
        decimal initialDeposit,
        int baseCandidateCount,
        int searchCandidateCount)
    {
        var baseCandidates = StrategyCandidateGenerator.Generate(
            asset,
            timeframe,
            baseCandidateCount,
            "base");
        var baseEvaluation = await LoadOrEvaluateBaseCandidatesAsync(
            asset,
            timeframe,
            baseCandidates,
            periods,
            initialDeposit);
        var baseResults = baseEvaluation.Results;
        var folds = new List<(
            WalkForwardFoldResult Report,
            CandidatePeriodBacktest Backtest,
            CandidatePeriodBacktest BaselineBacktest)>();

        // At least two earlier calendar periods are required before an outer test.
        for (int testPeriodIndex = 2; testPeriodIndex < periods.Count; testPeriodIndex++)
        {
            WalkForwardPeriod testPeriod = periods[testPeriodIndex];
            var trainingPeriods = periods.Take(testPeriodIndex).ToList();
            var trainingRows = CreateTrainingRows(baseCandidates, baseResults, trainingPeriods);
            var baselineSelection = SelectBaselineCandidate(trainingRows);
            var baselineEvaluated = baseResults.Single(result =>
                result.CandidateIndex == baselineSelection.CandidateIndex && result.Period == testPeriod);

            var model = _machineLearningService.TrainWalkForwardModel(trainingRows);
            var predicted = _machineLearningService.PredictBestWalkForwardCandidate(
                model,
                asset,
                timeframe,
                searchCandidateCount,
                $"outer-{testPeriod.Year}");
            var evaluated = (await _backtestService.EvaluateCandidatePeriodsAsync(
                asset,
                timeframe,
                new[] { (Index: testPeriod.Year, Parameters: predicted.Parameters) },
                new[] { testPeriod },
                initialDeposit)).Single();

            _logger.LogInformation(
                "Walk-forward fold completed. Asset: {Asset}, Timeframe: {Timeframe}, TestYear: {TestYear}, RiskEligibleCandidates: {RiskEligibleCandidates}/{CandidatesEvaluated}, UsedFallback: {UsedFallback}, PredictedScore: {PredictedScore}, ActualReturn: {ActualReturn}, ActualDrawdown: {ActualDrawdown}",
                asset,
                timeframe,
                testPeriod.Year,
                predicted.RiskPrediction.CandidatesPassedRiskFilter,
                predicted.RiskPrediction.CandidatesEvaluated,
                predicted.RiskPrediction.UsedFallback,
                predicted.PredictedScore,
                evaluated.TotalReturn,
                evaluated.MaxDrawdown);

            folds.Add((new WalkForwardFoldResult
            {
                TestYear = testPeriod.Year,
                TrainingStart = trainingPeriods[0].Start,
                TrainingEnd = testPeriod.Start,
                TestStart = testPeriod.Start,
                TestEnd = testPeriod.End,
                IsPartialPeriod = testPeriod.IsPartial,
                PredictedScore = predicted.PredictedScore,
                Prediction = predicted.RiskPrediction,
                PredictionError = CreatePredictionError(predicted.RiskPrediction, evaluated),
                TotalTrades = evaluated.TotalTrades,
                TotalReturn = Math.Round(evaluated.TotalReturn, 2),
                MaxDrawdown = Math.Round(evaluated.MaxDrawdown, 2),
                ProfitFactor = ToNullableProfitFactor(evaluated.ProfitFactor),
                SharpeRatio = Math.Round(evaluated.SharpeRatio, 2),
                Parameters = predicted.Parameters,
                Baseline = new WalkForwardFoldBenchmark
                {
                    CandidateIndex = baselineSelection.CandidateIndex,
                    TrainingScore = Math.Round(baselineSelection.TrainingScore, 2),
                    TotalTrades = baselineEvaluated.TotalTrades,
                    TotalReturn = Math.Round(baselineEvaluated.TotalReturn, 2),
                    MaxDrawdown = Math.Round(baselineEvaluated.MaxDrawdown, 2),
                    ProfitFactor = ToNullableProfitFactor(baselineEvaluated.ProfitFactor),
                    SharpeRatio = Math.Round(baselineEvaluated.SharpeRatio, 2),
                    Parameters = baselineEvaluated.Parameters
                }
            }, evaluated, baselineEvaluated));
        }

        var finalRows = CreateTrainingRows(baseCandidates, baseResults, periods);
        var finalModel = _machineLearningService.TrainWalkForwardModel(finalRows);
        var finalCandidate = _machineLearningService.PredictBestWalkForwardCandidate(
            finalModel,
            asset,
            timeframe,
            searchCandidateCount,
            $"final-{periods[^1].End:yyyyMMdd}");

        return CreateGroupResult(asset, timeframe, folds, finalCandidate, baseEvaluation.DataQuality);
    }

    private async Task<(List<CandidatePeriodBacktest> Results, HistoricalDataQualityResult DataQuality)>
        LoadOrEvaluateBaseCandidatesAsync(
            string asset,
            string timeframe,
            IReadOnlyList<OptimizationInput> candidates,
            IReadOnlyList<WalkForwardPeriod> periods,
            decimal initialDeposit)
    {
        DateTime firstStart = periods.Min(period => period.Start);
        DateTime lastEnd = periods.Max(period => period.End);
        var cachedRows = await _dbContext.OptimizationRuns
            .AsNoTracking()
            .Where(run => run.Asset == asset &&
                          run.Timeframe == timeframe &&
                          run.CandidateSetVersion == StrategyCandidateGenerator.CandidateSetVersion &&
                          run.EngineVersion == BacktestService.BacktestEngineVersion &&
                          run.TradingFee == BacktestService.TradingFee &&
                          run.Slippage == BacktestService.ExecutionSlippage &&
                          run.PeriodStart >= firstStart &&
                          run.PeriodEnd <= lastEnd &&
                          run.CandidateIndex != null)
            .ToListAsync();
        var cachedKeys = cachedRows
            .Where(row => row.PeriodStart.HasValue && row.PeriodEnd.HasValue)
            .ToDictionary(
                row => (row.CandidateIndex!.Value, row.PeriodStart!.Value, row.PeriodEnd!.Value),
                row => row);
        var candidateIndexesToEvaluate = Enumerable.Range(0, candidates.Count)
            .Where(candidateIndex => periods.Any(period =>
                !cachedKeys.ContainsKey((candidateIndex, period.Start, period.End))))
            .ToList();
        HistoricalDataQualityResult dataQuality = null;

        if (candidateIndexesToEvaluate.Count > 0)
        {
            var evaluated = await _backtestService.EvaluateCandidatePeriodsAsync(
                asset,
                timeframe,
                candidateIndexesToEvaluate.Select(index => (Index: index, Parameters: candidates[index])).ToList(),
                periods,
                initialDeposit);
            dataQuality = evaluated.FirstOrDefault()?.DataQuality;
            var rowsToAdd = evaluated
                .Where(result => !cachedKeys.ContainsKey(
                    (result.CandidateIndex, result.Period.Start, result.Period.End)))
                .Select(result => ToOptimizationRun(asset, timeframe, result))
                .ToList();

            await _dbContext.OptimizationRuns.AddRangeAsync(rowsToAdd);
            await _dbContext.SaveChangesAsync();
            foreach (var row in rowsToAdd)
                cachedKeys[(row.CandidateIndex!.Value, row.PeriodStart!.Value, row.PeriodEnd!.Value)] = row;
            _dbContext.ChangeTracker.Clear();
        }

        dataQuality ??= await _backtestService.GetHistoricalDataQualityAsync(
            asset,
            timeframe,
            firstStart,
            lastEnd);
        var results = (from candidateIndex in Enumerable.Range(0, candidates.Count)
                from period in periods
                let row = cachedKeys[(candidateIndex, period.Start, period.End)]
                select FromOptimizationRun(candidateIndex, candidates[candidateIndex], period, row))
            .ToList();
        return (results, dataQuality);
    }

    internal static List<OptimizationInput> CreateTrainingRows(
        IReadOnlyList<OptimizationInput> candidates,
        IReadOnlyCollection<CandidatePeriodBacktest> results,
        IReadOnlyCollection<WalkForwardPeriod> allowedPeriods)
    {
        return candidates
            .Select((candidate, candidateIndex) =>
            {
                var targets = CalculateTrainingTargets(results
                    .Where(result => result.CandidateIndex == candidateIndex &&
                                     allowedPeriods.Contains(result.Period))
                    .ToList());
                return candidate.CopyWithWalkForwardTargets(
                    (float)targets.Score,
                    (float)targets.AnnualReturn,
                    (float)targets.MaxDrawdown,
                    (float)targets.ProfitFactor,
                    (float)targets.AnnualTrades);
            })
            .ToList();
    }

    internal static decimal CalculateTrainingScore(IReadOnlyCollection<CandidatePeriodBacktest> results) =>
        CalculateTrainingTargets(results).Score;

    internal static WalkForwardTrainingTargets CalculateTrainingTargets(
        IReadOnlyCollection<CandidatePeriodBacktest> results)
    {
        if (results.Count == 0)
            return new WalkForwardTrainingTargets(-500m, -100m, 100m, 0m, 0m);

        decimal compoundedReturn = CompoundReturns(results.Select(result => result.TotalReturn));
        decimal years = CalculateYears(results.Select(result => result.Period));
        decimal annualReturn = AnnualizeReturn(compoundedReturn, years);
        decimal maximumDrawdown = results.Max(result => result.MaxDrawdown);
        int totalTrades = results.Sum(result => result.TotalTrades);
        decimal annualTrades = years <= 0m ? 0m : totalTrades / years;
        decimal profitFactor = CalculateProfitFactor(
            results.Sum(result => result.GrossProfit),
            results.Sum(result => result.GrossLoss));
        decimal trainingProfitFactor = profitFactor == decimal.MaxValue ? 10m : Math.Min(profitFactor, 10m);
        int positivePeriods = results.Count(result => result.TotalReturn > 0m);
        int requiredPositivePeriods = (int)Math.Ceiling(results.Count * 0.6m);
        decimal positiveProfit = results.Where(result => result.TotalReturn > 0m).Sum(result => result.TotalReturn);
        decimal bestPositiveReturn = results.Max(result => Math.Max(0m, result.TotalReturn));
        decimal concentration = positiveProfit <= 0m ? 100m : bestPositiveReturn / positiveProfit * 100m;
        decimal tradePenalty = Math.Max(0m, MachineLearningService.PreferredTrainingTrades - annualTrades) *
                               MachineLearningService.MissingTradePenalty;
        decimal consistencyPenalty = Math.Max(0, requiredPositivePeriods - positivePeriods) * 20m;
        decimal concentrationPenalty = Math.Max(0m, concentration - MaximumProfitConcentration) * 0.5m;

        decimal score = annualReturn
                        - MachineLearningService.DrawdownPenaltyWeight * maximumDrawdown
                        - tradePenalty
                        - consistencyPenalty
                        - concentrationPenalty;
        return new WalkForwardTrainingTargets(
            score,
            annualReturn,
            maximumDrawdown,
            trainingProfitFactor,
            annualTrades);
    }

    internal static (int CandidateIndex, decimal TrainingScore) SelectBaselineCandidate(
        IReadOnlyList<OptimizationInput> trainingRows)
    {
        if (trainingRows.Count == 0)
            throw new ArgumentException("At least one training row is required.", nameof(trainingRows));

        return trainingRows
            .Select((row, index) => (CandidateIndex: index, TrainingScore: (decimal)row.RiskAdjustedScore))
            .OrderByDescending(candidate => candidate.TrainingScore)
            .ThenBy(candidate => candidate.CandidateIndex)
            .First();
    }

    private static WalkForwardStrategyResult CreateGroupResult(
        string asset,
        string timeframe,
        IReadOnlyCollection<(
            WalkForwardFoldResult Report,
            CandidatePeriodBacktest Backtest,
            CandidatePeriodBacktest BaselineBacktest)> folds,
        WalkForwardCandidatePrediction finalCandidate,
        HistoricalDataQualityResult dataQuality)
    {
        decimal totalReturn = CompoundReturns(folds.Select(fold => fold.Backtest.TotalReturn));
        decimal maxDrawdown = folds.Count == 0 ? 0m : folds.Max(fold => fold.Backtest.MaxDrawdown);
        decimal grossProfit = folds.Sum(fold => fold.Backtest.GrossProfit);
        decimal grossLoss = folds.Sum(fold => fold.Backtest.GrossLoss);
        decimal profitFactor = 0m;
        if (grossLoss > 0m)
            profitFactor = grossProfit / grossLoss;
        else if (grossProfit > 0m)
            profitFactor = decimal.MaxValue;
        int totalTrades = folds.Sum(fold => fold.Backtest.TotalTrades);
        int positiveFolds = folds.Count(fold => fold.Backtest.TotalReturn > 0m);
        int requiredPositiveFolds = (int)Math.Ceiling(folds.Count * 0.75m);
        decimal positiveProfit = folds.Where(fold => fold.Backtest.TotalReturn > 0m)
            .Sum(fold => fold.Backtest.TotalReturn);
        decimal bestPositiveReturn = folds.Count == 0
            ? 0m
            : folds.Max(fold => Math.Max(0m, fold.Backtest.TotalReturn));
        decimal concentration = positiveProfit <= 0m ? 0m : bestPositiveReturn / positiveProfit * 100m;
        decimal weightedSharpe = totalTrades == 0
            ? 0m
            : folds.Sum(fold => fold.Backtest.SharpeRatio * fold.Backtest.TotalTrades) / totalTrades;
        decimal score = totalReturn - MachineLearningService.DrawdownPenaltyWeight * maxDrawdown;
        decimal baselineReturn = CompoundReturns(folds.Select(fold => fold.BaselineBacktest.TotalReturn));
        decimal baselineMaxDrawdown = folds.Count == 0
            ? 0m
            : folds.Max(fold => fold.BaselineBacktest.MaxDrawdown);
        decimal baselineGrossProfit = folds.Sum(fold => fold.BaselineBacktest.GrossProfit);
        decimal baselineGrossLoss = folds.Sum(fold => fold.BaselineBacktest.GrossLoss);
        decimal baselineProfitFactor = CalculateProfitFactor(baselineGrossProfit, baselineGrossLoss);
        int baselineTrades = folds.Sum(fold => fold.BaselineBacktest.TotalTrades);
        decimal baselineSharpe = baselineTrades == 0
            ? 0m
            : folds.Sum(fold => fold.BaselineBacktest.SharpeRatio * fold.BaselineBacktest.TotalTrades) /
              baselineTrades;
        decimal baselineScore = baselineReturn -
                                MachineLearningService.DrawdownPenaltyWeight * baselineMaxDrawdown;
        var rejectionReasons = new List<string>();

        if (folds.Count < 3) rejectionReasons.Add("fewer than 3 outer walk-forward folds");
        if (totalTrades < MinimumOuterTrades) rejectionReasons.Add($"fewer than {MinimumOuterTrades} outer trades");
        if (totalReturn <= 0m) rejectionReasons.Add("non-positive compounded outer return");
        if (positiveFolds < requiredPositiveFolds)
            rejectionReasons.Add($"positive in fewer than {requiredPositiveFolds} outer folds");
        if (profitFactor < MinimumProfitFactor)
            rejectionReasons.Add($"outer profit factor below {MinimumProfitFactor:0.##}");
        if (weightedSharpe < MinimumSharpe)
            rejectionReasons.Add($"outer Sharpe ratio below {MinimumSharpe:0.##}");
        if (maxDrawdown > MaximumDrawdown)
            rejectionReasons.Add($"outer drawdown exceeds {MaximumDrawdown:0.##}%");
        if (maxDrawdown > totalReturn)
            rejectionReasons.Add("outer drawdown exceeds outer return");
        if (concentration > MaximumProfitConcentration)
            rejectionReasons.Add("more than 50% of positive outer profit comes from one fold");
        int unsafeFoldSelections = folds.Count(fold => !fold.Report.Prediction.RiskFilterPassed);
        if (unsafeFoldSelections > 0)
            rejectionReasons.Add($"no risk-eligible ML candidate in {unsafeFoldSelections} outer folds");
        if (!finalCandidate.RiskPrediction.RiskFilterPassed)
            rejectionReasons.Add("final ML candidate failed predicted risk filter");

        return new WalkForwardStrategyResult
        {
            Asset = asset,
            Timeframe = timeframe,
            IsAccepted = rejectionReasons.Count == 0,
            RejectionReasons = rejectionReasons,
            TotalTrades = totalTrades,
            TotalReturn = Math.Round(totalReturn, 2),
            MaxDrawdown = Math.Round(maxDrawdown, 2),
            ProfitFactor = ToNullableProfitFactor(profitFactor),
            SharpeRatio = Math.Round(weightedSharpe, 2),
            PositiveFolds = positiveFolds,
            RequiredPositiveFolds = requiredPositiveFolds,
            BestFoldProfitShare = Math.Round(concentration, 2),
            WalkForwardScore = Math.Round(score, 2),
            FinalCandidate = finalCandidate.Parameters,
            FinalPredictedScore = finalCandidate.PredictedScore,
            FinalPrediction = finalCandidate.RiskPrediction,
            DataQuality = dataQuality,
            Baseline = new WalkForwardBenchmarkSummary
            {
                TotalTrades = baselineTrades,
                TotalReturn = Math.Round(baselineReturn, 2),
                MaxDrawdown = Math.Round(baselineMaxDrawdown, 2),
                ProfitFactor = ToNullableProfitFactor(baselineProfitFactor),
                SharpeRatio = Math.Round(baselineSharpe, 2),
                WalkForwardScore = Math.Round(baselineScore, 2),
                MlReturnDelta = Math.Round(totalReturn - baselineReturn, 2),
                MlScoreDelta = Math.Round(score - baselineScore, 2),
                MlOutperformedBaseline = score > baselineScore
            },
            Folds = folds.Select(fold => fold.Report).ToList()
        };
    }

    private async Task ActivateAsync(WalkForwardAnalysisReport report)
    {
        var activeStrategies = report.Strategies
            .Where(strategy => strategy.IsAccepted)
            .Select(strategy =>
            {
                var active = StrategyValidationService.CreateActiveStrategy(
                    strategy.Asset,
                    strategy.Timeframe,
                    strategy.FinalCandidate);
                active.SelectionMethod = "WalkForward";
                active.CandidateSetVersion = report.CandidateSetVersion;
                active.TrainingStart = report.StartDate;
                active.TrainingEnd = report.EndDate;
                active.WalkForwardScore = strategy.WalkForwardScore;
                return active;
            })
            .ToList();

        var evaluatedKeys = report.Strategies
            .Select(strategy => (strategy.Asset, strategy.Timeframe))
            .ToHashSet();
        var existing = await _dbContext.ActiveStrategies
            .Where(strategy => strategy.SelectionMethod == "WalkForward")
            .ToListAsync();
        _dbContext.ActiveStrategies.RemoveRange(existing.Where(strategy =>
            evaluatedKeys.Contains((strategy.Asset, strategy.Timeframe))));
        await _dbContext.ActiveStrategies.AddRangeAsync(activeStrategies);
        await _dbContext.SaveChangesAsync();
        await _strategyProvider.LoadStrategiesFromDatabaseAsync();
        report.ActiveStrategiesCreated = activeStrategies.Count;
    }

    private static OptimizationRun ToOptimizationRun(
        string asset,
        string timeframe,
        CandidatePeriodBacktest result) => new()
    {
        Asset = asset,
        Timeframe = timeframe,
        IndicatorWeightsJson = StrategyParameterSerializer.Serialize(result.Parameters),
        TotalProfitPercent = result.TotalReturn,
        TotalTrades = result.TotalTrades,
        WinningTrades = result.WinningTrades,
        MaxDrawdown = result.MaxDrawdown,
        ProfitFactor = result.ProfitFactor,
        SharpeRatio = result.SharpeRatio,
        GrossProfit = result.GrossProfit,
        GrossLoss = result.GrossLoss,
        PeriodStart = result.Period.Start,
        PeriodEnd = result.Period.End,
        CandidateSetVersion = StrategyCandidateGenerator.CandidateSetVersion,
        CandidateIndex = result.CandidateIndex,
        TradingFee = BacktestService.TradingFee,
        Slippage = BacktestService.ExecutionSlippage,
        EngineVersion = BacktestService.BacktestEngineVersion,
        RunDate = DateTime.UtcNow
    };

    private static CandidatePeriodBacktest FromOptimizationRun(
        int candidateIndex,
        OptimizationInput parameters,
        WalkForwardPeriod period,
        OptimizationRun row) => new()
    {
        CandidateIndex = candidateIndex,
        Parameters = parameters,
        Period = period,
        TotalTrades = row.TotalTrades,
        WinningTrades = row.WinningTrades,
        TotalReturn = row.TotalProfitPercent,
        MaxDrawdown = row.MaxDrawdown,
        ProfitFactor = row.ProfitFactor,
        SharpeRatio = row.SharpeRatio,
        GrossProfit = row.GrossProfit,
        GrossLoss = row.GrossLoss
    };

    private static List<WalkForwardPeriod> CreatePeriods(DateTime startDate, DateTime endDate)
    {
        var periods = new List<WalkForwardPeriod>();
        int lastYear = endDate.AddTicks(-1).Year;
        for (int year = startDate.Year; year <= lastYear; year++)
        {
            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime start = startDate > yearStart ? startDate : yearStart;
            DateTime naturalEnd = yearStart.AddYears(1);
            DateTime end = endDate < naturalEnd ? endDate : naturalEnd;
            if (end > start)
                periods.Add(new WalkForwardPeriod(year, start, end, end < naturalEnd));
        }

        return periods;
    }

    private static decimal CompoundReturns(IEnumerable<decimal> returns)
    {
        decimal multiplier = 1m;
        foreach (decimal value in returns)
            multiplier *= 1m + value / 100m;
        return (multiplier - 1m) * 100m;
    }

    internal static decimal AnnualizeReturn(decimal totalReturn, decimal years)
    {
        if (years <= 0m) return 0m;
        decimal multiplier = 1m + totalReturn / 100m;
        if (multiplier <= 0m) return -100m;
        double annualized = Math.Pow((double)multiplier, 1d / (double)years) - 1d;
        return (decimal)annualized * 100m;
    }

    private static decimal CalculateYears(IEnumerable<WalkForwardPeriod> periods)
    {
        double days = periods
            .Distinct()
            .Sum(period => (period.End - period.Start).TotalDays);
        return (decimal)(days / 365.2425d);
    }

    private static WalkForwardPredictionError CreatePredictionError(
        WalkForwardRiskPrediction prediction,
        CandidatePeriodBacktest actual)
    {
        decimal years = CalculateYears(new[] { actual.Period });
        decimal actualAnnualReturn = AnnualizeReturn(actual.TotalReturn, years);
        decimal actualAnnualTrades = years <= 0m ? 0m : actual.TotalTrades / years;
        decimal? actualProfitFactor = ToNullableProfitFactor(actual.ProfitFactor);
        decimal? profitFactorError = prediction.PredictedProfitFactor.HasValue && actualProfitFactor.HasValue
            ? Math.Abs(prediction.PredictedProfitFactor.Value - actualProfitFactor.Value)
            : null;

        return new WalkForwardPredictionError
        {
            ActualAnnualizedReturn = Math.Round(actualAnnualReturn, 2),
            ActualMaxDrawdown = Math.Round(actual.MaxDrawdown, 2),
            ActualProfitFactor = actualProfitFactor,
            ActualAnnualizedTrades = Math.Round(actualAnnualTrades, 2),
            AnnualReturnAbsoluteError = Math.Round(
                Math.Abs(prediction.PredictedAnnualReturn - actualAnnualReturn),
                2),
            MaxDrawdownAbsoluteError = Math.Round(
                Math.Abs(prediction.PredictedMaxDrawdown - actual.MaxDrawdown),
                2),
            ProfitFactorAbsoluteError = profitFactorError.HasValue
                ? Math.Round(profitFactorError.Value, 2)
                : null,
            AnnualTradesAbsoluteError = Math.Round(
                Math.Abs(prediction.PredictedAnnualTrades - actualAnnualTrades),
                2)
        };
    }

    private static decimal CalculateProfitFactor(decimal grossProfit, decimal grossLoss)
    {
        if (grossLoss > 0m) return grossProfit / grossLoss;
        return grossProfit > 0m ? decimal.MaxValue : 0m;
    }

    private static string CanonicalFilter(string value, IReadOnlyCollection<string> supported, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string canonical = supported.FirstOrDefault(candidate =>
            string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        if (canonical == null)
            throw new ArgumentException(
                $"Unsupported {name} '{value}'. Supported values: {string.Join(", ", supported)}.",
                name);
        return canonical;
    }

    private static decimal? ToNullableProfitFactor(decimal value) =>
        value == decimal.MaxValue ? null : Math.Round(value, 2);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}