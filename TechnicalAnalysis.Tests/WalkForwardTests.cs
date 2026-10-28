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
