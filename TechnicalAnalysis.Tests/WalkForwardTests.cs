using FluentAssertions;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TechnicalAnalysis.Domain.Services;

namespace TechnicalAnalysis.Tests;

public sealed class WalkForwardTests
{
    [Fact]
    public void CandidateGenerator_WithSameScope_ShouldBeReproducible()
    {
        var first = StrategyCandidateGenerator.Generate("ETHUSDT", "FourHour", 20, "fold-2025");
        var second = StrategyCandidateGenerator.Generate("ETHUSDT", "FourHour", 20, "fold-2025");

        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
    }

    [Fact]
    public void CandidateGenerator_ShouldLimitIndicatorComplexity()
    {
        var candidate = StrategyCandidateGenerator.Generate("SOLUSDT", "OneHour", 1, "base").Single();
        float[] oscillators =
        {
            candidate.RsiWeight, candidate.StochasticKWeight, candidate.CciWeight, candidate.AdxWeight,
            candidate.AoWeight, candidate.MomentumWeight, candidate.MacdWeight, candidate.UltimateWeight,
            candidate.WilliamsRWeight, candidate.BullBearWeight, candidate.StochasticRsiWeight
        };
        float[] movingAverages =
        {
            candidate.Sma10Weight, candidate.Ema10Weight, candidate.Sma20Weight, candidate.Ema20Weight,
            candidate.Sma30Weight, candidate.Ema30Weight, candidate.Sma50Weight, candidate.Ema50Weight,
            candidate.Sma100Weight, candidate.Ema100Weight, candidate.Sma200Weight, candidate.Ema200Weight,
            candidate.Hma9Weight
        };

        oscillators.Count(weight => weight > 0f).Should().Be(4);
        movingAverages.Count(weight => weight > 0f).Should().Be(4);
        oscillators.Sum().Should().BeApproximately(1f, 0.0001f);
        movingAverages.Sum().Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void TrainingRows_ShouldIgnoreResultsOutsideAllowedPeriods()
    {
        var candidates = new[]
        {
            new OptimizationInput { Asset = "ETHUSDT", Timeframe = "FourHour" }
        };
        var period2021 = Period(2021);
        var period2022 = Period(2022);
        var futurePeriod = Period(2023);
        var results = new List<CandidatePeriodBacktest>
        {
            Result(0, candidates[0], period2021, 10m),
            Result(0, candidates[0], period2022, 5m),
            Result(0, candidates[0], futurePeriod, -90m)
        };

        float before = WalkForwardStrategyService.CreateTrainingRows(
            candidates,
            results,
            new[] { period2021, period2022 })[0].RiskAdjustedScore;
        results[^1].TotalReturn = 1_000m;
        float after = WalkForwardStrategyService.CreateTrainingRows(
            candidates,
            results,
            new[] { period2021, period2022 })[0].RiskAdjustedScore;

        after.Should().Be(before);
    }

    [Fact]
    public void GroupSelection_ShouldContainAllConfiguredCombinations()
    {
        var groups = WalkForwardStrategyService.CreateGroups(null, null);

        groups.Should().HaveCount(14);
        groups.Should().Contain(("SOLUSDT", "OneDay"));
    }

    [Fact]
    public void GroupSelection_ShouldApplyAssetAndTimeframeFilters()
    {
        var groups = WalkForwardStrategyService.CreateGroups("ethusdt", "fourhour");

        groups.Should().ContainSingle()
            .Which.Should().Be(("ETHUSDT", "FourHour"));
    }

    [Fact]
    public void BaselineSelection_ShouldUseTrainingScoresOnly()
    {
        var trainingRows = new[]
        {
            new OptimizationInput { RiskAdjustedScore = 10f },
            new OptimizationInput { RiskAdjustedScore = 25f },
            new OptimizationInput { RiskAdjustedScore = -5f }
        };

        var selected = WalkForwardStrategyService.SelectBaselineCandidate(trainingRows);

        selected.CandidateIndex.Should().Be(1);
        selected.TrainingScore.Should().Be(25m);
    }

    [Fact]
    public void TrainingTargets_ShouldNormalizeReturnAndTradesToAnnualHorizon()
    {
        var candidate = new OptimizationInput { Asset = "ETHUSDT", Timeframe = "FourHour" };
        var first = Result(0, candidate, Period(2021), 10m);
        var second = Result(0, candidate, Period(2022), 10m);
        first.GrossProfit = second.GrossProfit = 12m;
        first.GrossLoss = second.GrossLoss = 10m;

        WalkForwardTrainingTargets targets = WalkForwardStrategyService.CalculateTrainingTargets(
            new[] { first, second });

        targets.AnnualReturn.Should().BeApproximately(10m, 0.1m);
        targets.AnnualTrades.Should().BeApproximately(20m, 0.1m);
        targets.MaxDrawdown.Should().Be(5m);
        targets.ProfitFactor.Should().Be(1.2m);
    }

    [Fact]
    public void RiskPrediction_ShouldApplyAllSafetyGates()
    {
        WalkForwardRiskPrediction accepted = MachineLearningService.EvaluateRiskPrediction(30f, 20f, 1.3f, 30f);
        WalkForwardRiskPrediction rejected = MachineLearningService.EvaluateRiskPrediction(20f, 30f, 1.05f, 10f);

        accepted.RiskFilterPassed.Should().BeTrue();
        accepted.RejectionReasons.Should().BeEmpty();
        rejected.RiskFilterPassed.Should().BeFalse();
        rejected.RejectionReasons.Should().Contain("predicted drawdown exceeds 25%");
        rejected.RejectionReasons.Should().Contain("predicted profit factor below 1.15");
        rejected.RejectionReasons.Should().Contain("predicted annual trades below 15");
        rejected.RejectionReasons.Should().Contain("predicted annual return does not exceed predicted drawdown");
    }

    [Fact]
    public void RiskAwareModel_ShouldTrainAndScoreSearchCandidates()
    {
        var service = new MachineLearningService(null, null);
        var trainingRows = StrategyCandidateGenerator.Generate("ETHUSDT", "FourHour", 100, "risk-model-test")
            .Select((candidate, index) => candidate.CopyWithWalkForwardTargets(
                score: index,
                annualReturn: 30f + index / 10f,
                maxDrawdown: 10f + index / 100f,
                profitFactor: 1.3f,
                annualTrades: 30f))
            .ToList();

        WalkForwardModelBundle model = service.TrainWalkForwardModel(trainingRows);
        WalkForwardCandidatePrediction prediction = service.PredictBestWalkForwardCandidate(
            model,
            "ETHUSDT",
            "FourHour",
            100,
            "risk-model-search-test");

        prediction.Parameters.Should().NotBeNull();
        prediction.RiskPrediction.CandidatesEvaluated.Should().Be(100);
        prediction.RiskPrediction.PredictedAnnualReturn.Should().BeGreaterThan(0m);
        prediction.RiskPrediction.PredictedProfitFactor.Should().NotBeNull();
    }

    [Fact]
    public void DataQuality_ShouldAllowMissingRateAtConfiguredLimit()
    {
        DateTime start = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan interval = TimeSpan.FromMinutes(15);
        var openTimes = Enumerable.Range(0, 1000)
            .Where(index => index is < 200 or > 204)
            .Select(index => start.AddTicks(interval.Ticks * index))
            .ToList();

        HistoricalDataQualityResult quality = BinanceService.AnalyzeDataQuality(
            "ETHUSDT",
            "FifteenMinutes",
            openTimes,
            interval);

        quality.ActualCandles.Should().Be(995);
        quality.ExpectedCandles.Should().Be(1000);
        quality.MissingCandles.Should().Be(5);
        quality.MissingPercent.Should().Be(BinanceService.MaximumMissingCandlePercent);
        quality.MaximumGapCandles.Should().Be(5);
        quality.Gaps.Should().ContainSingle();
    }

    [Fact]
    public void DataQuality_ShouldRejectIndividualGapLongerThanOneDay()
    {
        DateTime start = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Action action = () => BinanceService.AnalyzeDataQuality(
            "ETHUSDT",
            "FifteenMinutes",
            new[] { start, start.AddHours(25) },
            TimeSpan.FromMinutes(15));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds 24 hours*");
    }

    [Fact]
    public void DataQuality_ShouldRejectMissingRateAboveConfiguredLimit()
    {
        DateTime start = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan interval = TimeSpan.FromMinutes(15);
        var openTimes = Enumerable.Range(0, 100)
            .Where(index => index != 50)
            .Select(index => start.AddTicks(interval.Ticks * index))
            .ToList();

        Action action = () => BinanceService.AnalyzeDataQuality(
            "ETHUSDT",
            "FifteenMinutes",
            openTimes,
            interval);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*limit is*");
    }

    private static WalkForwardPeriod Period(int year) => new(
        year,
        new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        false);

    private static CandidatePeriodBacktest Result(
        int index,
        OptimizationInput parameters,
        WalkForwardPeriod period,
        decimal totalReturn) => new()
    {
        CandidateIndex = index,
        Parameters = parameters,
        Period = period,
        TotalTrades = 20,
        TotalReturn = totalReturn,
        MaxDrawdown = 5m,
        ProfitFactor = 1.2m,
        SharpeRatio = 0.5m
    };
}