using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using FluentAssertions;
using TechnicalAnalysis.Domain.Models;
using TechnicalAnalysis.Domain.Services;

namespace TechnicalAnalysis.Tests;

public sealed class TournamentSignalTests
{
    [Fact]
    public void BuildSignal_WithAlignedUptrend_ShouldReturnSpotBuyPlan()
    {
        DateTime now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateTimeframes(now, bullish: true);

        TournamentSignalResult result = TournamentSignalService.BuildSignal("ETHUSDT", now, candles);

        result.Action.Should().Be("BUY");
        result.CanOpenNewSpotPosition.Should().BeTrue();
        result.ReferencePrice.Should().HaveValue();
        decimal entry = result.LimitPrice.GetValueOrDefault(result.ReferencePrice.GetValueOrDefault());
        result.StopLoss.Should().HaveValue();
        result.TakeProfit.Should().HaveValue();
        result.StopLoss.Value.Should().BeLessThan(entry);
        result.TakeProfit.Value.Should().BeGreaterThan(entry);
        result.RiskRewardRatio.Should().Be(2m);
        result.MaximumAccountRiskPercent.Should().Be(0.5m);
        result.IsStale.Should().BeFalse();
    }

    [Fact]
    public void BuildSignal_WithAlignedDowntrend_ShouldReturnExitOnlySellSignal()
    {
        DateTime now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateTimeframes(now, bullish: false);

        TournamentSignalResult result = TournamentSignalService.BuildSignal("SOLUSDT", now, candles);

        result.Action.Should().Be("SELL");
        result.CanOpenNewSpotPosition.Should().BeFalse();
        result.StopLoss.Should().BeNull();
        result.TakeProfit.Should().BeNull();
        result.Warning.Should().Contain("existing position");
    }

    [Fact]
    public void BuildSignal_WithStaleCandles_ShouldBlockTrading()
    {
        DateTime candleTime = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateTimeframes(candleTime, bullish: true);

        TournamentSignalResult result = TournamentSignalService.BuildSignal(
            "ETHUSDT",
            candleTime.AddMinutes(20),
            candles);

        result.Action.Should().Be("NO_TRADE");
        result.Confidence.Should().Be(0);
        result.CanOpenNewSpotPosition.Should().BeFalse();
        result.SuggestedOrderType.Should().BeNull();
        result.IsStale.Should().BeTrue();
    }

    [Fact]
    public void BuildSignal_WithConflictingTimeframes_ShouldReturnNoTrade()
    {
        DateTime now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateTimeframes(now, bullish: true);
        candles[KlineInterval.OneHour] = CreateCandles(now, 60, bullish: false);

        TournamentSignalResult result = TournamentSignalService.BuildSignal("SOLUSDT", now, candles);

        result.Action.Should().Be("NO_TRADE");
        result.SuggestedOrderType.Should().BeNull();
        result.StopLoss.Should().BeNull();
        result.TakeProfit.Should().BeNull();
    }

    [Fact]
    public void ParseSupportedAsset_ShouldRejectNonTournamentPair()
    {
        Action action = () => TournamentSignalService.ParseSupportedAsset("BTCUSDT");

        action.Should().Throw<ArgumentException>().WithMessage("*ETHUSDT and SOLUSDT*");
    }

    private static Dictionary<KlineInterval, IReadOnlyList<BinanceSpotKline>> CreateTimeframes(
        DateTime now,
        bool bullish) => new()
    {
        [KlineInterval.OneMinute] = CreateCandles(now, 1, bullish),
        [KlineInterval.FiveMinutes] = CreateCandles(now, 5, bullish),
        [KlineInterval.FifteenMinutes] = CreateCandles(now, 15, bullish),
        [KlineInterval.OneHour] = CreateCandles(now, 60, bullish)
    };

    private static IReadOnlyList<BinanceSpotKline> CreateCandles(
        DateTime now,
        int intervalMinutes,
        bool bullish)
    {
        const int count = 200;
        decimal price = 100m;
        var result = new List<BinanceSpotKline>(count);
        DateTime firstOpen = now.AddMinutes(-count * intervalMinutes);
        for (int index = 0; index < count; index++)
        {
            decimal movement = index % 2 == 0 ? 1m : -0.6m;
            if (!bullish) movement = -movement;
            decimal open = price;
            price += movement;
            result.Add(new BinanceSpotKline
            {
                OpenTime = firstOpen.AddMinutes(index * intervalMinutes),
                CloseTime = firstOpen.AddMinutes((index + 1) * intervalMinutes).AddMilliseconds(-1),
                OpenPrice = open,
                ClosePrice = price,
                HighPrice = Math.Max(open, price) + 0.2m,
                LowPrice = Math.Min(open, price) - 0.2m,
                Volume = index == count - 1 ? 160m : 100m
            });
        }

        return result;
    }
}
