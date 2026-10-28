using FluentAssertions;
using TechnicalAnalysis.Domain.Services.Backtesting;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Tests.Unit;

/// <summary>
/// Verifies deterministic execution and metrics without network or database dependencies.
/// </summary>
public class BacktestEngineTests
{
    /// <summary>
    /// Verifies that a close-based signal is executed at the next candle open.
    /// </summary>
    [Fact]
    public void Run_WithBuySignal_ShouldEnterAtNextCandleOpen()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 110m, 110m, 110m, 110m),
            Candle(2, 120m, 120m, 120m, 120m)
        };

        var result = BacktestEngine.Run(candles, 0, 1_000m, Settings(fee: 0m), trackTrades: true);

        result.Trades.Should().ContainSingle();
        result.Trades[0].EntryPrice.Should().Be(110m);
        result.Trades[0].ExitPrice.Should().Be(120m);
    }

    /// <summary>
    /// Verifies that a sell signal is also executed at the next candle open.
    /// </summary>
    [Fact]
    public void Run_WithSellSignal_ShouldExitAtNextCandleOpen()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 110m, 110m, 110m, 110m, signal: -1m),
            Candle(2, 90m, 90m, 90m, 90m)
        };

        var result = BacktestEngine.Run(candles, 0, 1_000m, Settings(fee: 0m), trackTrades: true);

        result.Trades.Should().ContainSingle();
        result.Trades[0].ExitPrice.Should().Be(90m);
        result.Trades[0].Type.Should().Be("Signal");
    }

    /// <summary>
    /// Verifies the conservative stop-first rule when both barriers are inside one candle.
    /// </summary>
    [Fact]
    public void Run_WhenStopAndTakeProfitAreHit_ShouldUseStopLoss()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 100m, 120m, 80m, 100m)
        };

        var result = BacktestEngine.Run(
            candles,
            0,
            1_000m,
            Settings(fee: 0m, stopLoss: 0.10m, takeProfit: 0.10m),
            trackTrades: true);

        result.Trades.Should().ContainSingle();
        result.Trades[0].ExitPrice.Should().Be(90m);
        result.Trades[0].Type.Should().Be("StopLoss");
    }

    /// <summary>
    /// Verifies that a gap through the stop is filled at the candle open rather than the stop level.
    /// </summary>
    [Fact]
    public void Run_WhenPriceGapsThroughStop_ShouldExitAtOpen()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 100m, 105m, 95m, 100m),
            Candle(2, 80m, 85m, 75m, 80m)
        };

        var result = BacktestEngine.Run(
            candles,
            0,
            1_000m,
            Settings(fee: 0m, stopLoss: 0.10m),
            trackTrades: true);

        result.Trades.Should().ContainSingle();
        result.Trades[0].ExitPrice.Should().Be(80m);
        result.Trades[0].Type.Should().Be("StopLoss");
    }

    /// <summary>
    /// Verifies that both entry and exit fees affect balance and net trade return.
    /// </summary>
    [Fact]
    public void Run_WithFlatPrice_ShouldApplyBothTradingFees()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 100m, 100m, 100m, 100m)
        };

        var result = BacktestEngine.Run(candles, 0, 1_000m, Settings(fee: 0.001m), trackTrades: true);

        result.FinalBalance.Should().Be(998.001m);
        result.Trades[0].ProfitPercent.Should().Be(-0.1999m);
    }

    /// <summary>
    /// Verifies that drawdown includes unrealized losses while a position is open.
    /// </summary>
    [Fact]
    public void Run_WithOpenPositionDrawdown_ShouldUseMarkToMarketEquity()
    {
        var candles = new[]
        {
            Candle(0, 100m, 100m, 100m, 100m, signal: 1m),
            Candle(1, 100m, 100m, 100m, 100m),
            Candle(2, 100m, 100m, 50m, 50m),
            Candle(3, 50m, 100m, 50m, 100m)
        };

        var result = BacktestEngine.Run(
            candles,
            0,
            1_000m,
            Settings(fee: 0m, stopLoss: 0.90m),
            trackTrades: true);

        result.MaxDrawdown.Should().Be(50m);
        result.FinalBalance.Should().Be(1_000m);
    }

    /// <summary>
    /// Verifies compounded return and trade-equity drawdown calculations.
    /// </summary>
    [Fact]
    public void TradeMetrics_ShouldCompoundReturns()
    {
        var trades = new[]
        {
            new TradeRecord { ExitTime = DateTime.UnixEpoch, ProfitPercent = 10m },
            new TradeRecord { ExitTime = DateTime.UnixEpoch.AddDays(1), ProfitPercent = -10m }
        };

        BacktestEngine.CalculateCompoundedReturn(trades).Should().Be(-1m);
        BacktestEngine.CalculateTradeMaxDrawdown(trades).Should().Be(10m);
    }

    /// <summary>
    /// Verifies annualization of daily equity returns for a continuously traded crypto market.
    /// </summary>
    [Fact]
    public void CalculateAnnualizedSharpe_WithDailyEquity_ShouldUseThreeHundredSixtyFiveDays()
    {
        var dailyEquity = new[] { 110m, 99m, 118.8m };

        decimal result = BacktestEngine.CalculateAnnualizedSharpe(dailyEquity, 100m);

        result.Should().BeApproximately(8.3381m, 0.0001m);
    }

    /// <summary>
    /// Verifies validation of capital before a simulation starts.
    /// </summary>
    [Fact]
    public void Run_WithNonPositiveDeposit_ShouldRejectInput()
    {
        var candles = new[] { Candle(0, 100m, 100m, 100m, 100m) };

        Action action = () => BacktestEngine.Run(candles, 0, 0m, Settings(), trackTrades: true);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static BacktestCandleSnapshot Candle(
        int day,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal signal = 0m)
    {
        var oscillatorActions = new decimal[11];
        oscillatorActions[0] = signal;

        return new BacktestCandleSnapshot
        {
            OpenTime = DateTime.UnixEpoch.AddDays(day),
            OpenPrice = open,
            HighPrice = high,
            LowPrice = low,
            ClosePrice = close,
            OscillatorActions = oscillatorActions,
            MovingAverageActions = new decimal[13]
        };
    }

    private static BacktestSettings Settings(
        decimal fee = 0m,
        decimal stopLoss = 0.50m,
        decimal takeProfit = 10m)
    {
        var oscillatorWeights = new decimal[11];
        oscillatorWeights[0] = 1m;

        return new BacktestSettings(
            oscillatorWeights,
            new decimal[13],
            OscillatorGroupWeight: 1m,
            MovingAverageGroupWeight: 0m,
            BuyThreshold: 50,
            SellThreshold: -50,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            TradingFee: fee);
    }
}
