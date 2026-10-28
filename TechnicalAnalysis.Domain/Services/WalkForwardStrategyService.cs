using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services;

public sealed class WalkForwardStrategyService
{
    private const int MinimumOuterTrades = 60;
    private const decimal MinimumProfitFactor = 1.15m;
    private const decimal MinimumSharpe = 0.5m;
    private const decimal MaximumDrawdown = 25m;
    private const decimal MaximumProfitConcentration = 50m;

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
        bool activate = false)
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

        var storedGroups = await _dbContext.OptimizationRuns
            .AsNoTracking()
            .Where(run => run.Asset != null && run.Timeframe != null)
            .Select(run => new { run.Asset, run.Timeframe })
            .Distinct()
            .OrderBy(group => group.Asset)
            .ThenBy(group => group.Timeframe)
            .ToListAsync();
        var groups = storedGroups
            .Select(group => (group.Asset, group.Timeframe))
            .ToList();
        if (groups.Count == 0)
        {
            string[] defaultAssets = { "ETHUSDT", "SOLUSDT" };
            string[] defaultTimeframes =
            {
                "OneMinute", "FiveMinutes", "FifteenMinutes", "ThirtyMinutes",
                "OneHour", "FourHour", "OneDay"
            };
            groups = defaultAssets
                .SelectMany(asset => defaultTimeframes.Select(timeframe => (Asset: asset, Timeframe: timeframe)))
                .ToList();
        }

        var report = new WalkForwardAnalysisReport
        {
            StartDate = startDate,
            EndDate = endDate,
            InitialDeposit = initialDeposit,
            BaseCandidatesPerStrategy = baseCandidatesPerStrategy,
            SearchCandidatesPerFold = searchCandidatesPerFold,
            CandidateSetVersion = StrategyCandidateGenerator.CandidateSetVersion,
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
        }

        report.Strategies = report.Strategies
            .OrderByDescending(strategy => strategy.IsAccepted)
            .ThenByDescending(strategy => strategy.WalkForwardScore)
            .ThenBy(strategy => strategy.Asset)
            .ThenBy(strategy => strategy.Timeframe)
            .ToList();

        if (activate)
            await ActivateAsync(report);

        return report;
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
        var folds = new List<(WalkForwardFoldResult Report, CandidatePeriodBacktest Backtest)>();

        // At least two earlier calendar periods are required before an outer test.
        for (int testPeriodIndex = 2; testPeriodIndex < periods.Count; testPeriodIndex++)
        {
            WalkForwardPeriod testPeriod = periods[testPeriodIndex];
            var trainingPeriods = periods.Take(testPeriodIndex).ToList();
            var trainingRows = CreateTrainingRows(baseCandidates, baseResults, trainingPeriods);

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

            folds.Add((new WalkForwardFoldResult
            {
                TestYear = testPeriod.Year,
                TrainingStart = trainingPeriods[0].Start,
                TrainingEnd = testPeriod.Start,
                TestStart = testPeriod.Start,
                TestEnd = testPeriod.End,
                IsPartialPeriod = testPeriod.IsPartial,
                PredictedScore = predicted.PredictedScore,
                TotalTrades = evaluated.TotalTrades,
                TotalReturn = Math.Round(evaluated.TotalReturn, 2),
                MaxDrawdown = Math.Round(evaluated.MaxDrawdown, 2),
                ProfitFactor = ToNullableProfitFactor(evaluated.ProfitFactor),
                SharpeRatio = Math.Round(evaluated.SharpeRatio, 2),
                Parameters = predicted.Parameters
            }, evaluated));
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
            .Select((candidate, candidateIndex) => candidate.CopyWithScore(
                (float)CalculateTrainingScore(results
                    .Where(result => result.CandidateIndex == candidateIndex &&
                                     allowedPeriods.Contains(result.Period))
                    .ToList())))
            .ToList();
    }

    internal static decimal CalculateTrainingScore(IReadOnlyCollection<CandidatePeriodBacktest> results)
    {
        if (results.Count == 0)
            return -500m;

        decimal compoundedReturn = CompoundReturns(results.Select(result => result.TotalReturn));
        decimal maximumDrawdown = results.Max(result => result.MaxDrawdown);
        int totalTrades = results.Sum(result => result.TotalTrades);
        int positivePeriods = results.Count(result => result.TotalReturn > 0m);
        int requiredPositivePeriods = (int)Math.Ceiling(results.Count * 0.6m);
        decimal positiveProfit = results.Where(result => result.TotalReturn > 0m).Sum(result => result.TotalReturn);
        decimal bestPositiveReturn = results.Max(result => Math.Max(0m, result.TotalReturn));
        decimal concentration = positiveProfit <= 0m ? 100m : bestPositiveReturn / positiveProfit * 100m;
        decimal tradePenalty = Math.Max(0, MachineLearningService.PreferredTrainingTrades - totalTrades) *
                               MachineLearningService.MissingTradePenalty;
        decimal consistencyPenalty = Math.Max(0, requiredPositivePeriods - positivePeriods) * 20m;
        decimal concentrationPenalty = Math.Max(0m, concentration - MaximumProfitConcentration) * 0.5m;

        return compoundedReturn
               - MachineLearningService.DrawdownPenaltyWeight * maximumDrawdown
               - tradePenalty
               - consistencyPenalty
               - concentrationPenalty;
    }

    private static WalkForwardStrategyResult CreateGroupResult(
        string asset,
        string timeframe,
        IReadOnlyCollection<(WalkForwardFoldResult Report, CandidatePeriodBacktest Backtest)> folds,
        PredictedStrategyCandidate finalCandidate,
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
            DataQuality = dataQuality,
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

        var existing = await _dbContext.ActiveStrategies.ToListAsync();
        _dbContext.ActiveStrategies.RemoveRange(existing);
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

    private static decimal? ToNullableProfitFactor(decimal value) =>
        value == decimal.MaxValue ? null : Math.Round(value, 2);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}