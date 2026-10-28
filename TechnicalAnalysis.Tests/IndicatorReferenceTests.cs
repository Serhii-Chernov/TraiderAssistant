using Binance.Net.Objects.Models.Spot;
using FluentAssertions;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Tests.Unit;

/// <summary>
/// Verifies indicator calculations against deterministic reference formulas and boundary cases.
/// </summary>
public class IndicatorReferenceTests
{
    /// <summary>
    /// Verifies Wilder RSI against the commonly used 14-period reference series.
    /// </summary>
    [Fact]
    public void Rsi_WithWilderReferenceSeries_ShouldMatchExpectedValue()
    {
        decimal[] prices =
        {
            44.34m, 44.09m, 44.15m, 43.61m, 44.33m,
            44.83m, 45.10m, 45.42m, 45.84m, 46.08m,
            45.89m, 46.03m, 45.61m, 46.28m, 46.28m
        };

        var result = new RsiOscillator().Calculate(CreateKlines(prices));

        result.Value.Should().BeApproximately(70.4641m, 0.0001m);
    }

    /// <summary>
    /// Verifies that a flat market produces neutral RSI instead of an oversold signal.
    /// </summary>
    [Fact]
    public void Rsi_WithFlatPrices_ShouldReturnNeutralValue()
    {
        var result = new RsiOscillator().Calculate(CreateKlines(Enumerable.Repeat(100m, 20)));

        result.Value.Should().Be(50m);
        result.Action.Should().Be(TradeAction.Neutral);
    }

    /// <summary>
    /// Verifies that fast and slow EMA values are aligned to the same source candle in MACD.
    /// </summary>
    [Fact]
    public void Macd_ShouldMatchAlignedEmaReferenceCalculation()
    {
        var prices = Enumerable.Range(1, 60)
            .Select(value => (decimal)(value * value + value % 3 * 5))
            .ToList();
        decimal expectedHistogram = CalculateMacdHistogram(prices, 12, 26, 9);

        var result = new MacdOscillator().Calculate(CreateKlines(prices));

        result.Value.Should().BeApproximately(expectedHistogram, 0.000001m);
    }

    /// <summary>
    /// Verifies that Awesome Oscillator is based on candle median prices rather than closes.
    /// </summary>
    [Fact]
    public void AwesomeOscillator_ShouldUseMedianPrices()
    {
        var klines = Enumerable.Range(1, 40)
            .Select(index => new BinanceSpotKline
            {
                OpenTime = DateTime.UnixEpoch.AddMinutes(index),
                HighPrice = index * 3m + 10m,
                LowPrice = index,
                ClosePrice = 100m
            })
            .ToList();
        var medianPrices = klines.Select(kline => (kline.HighPrice + kline.LowPrice) / 2m).ToList();
        decimal expected = medianPrices.TakeLast(5).Average() - medianPrices.TakeLast(34).Average();

        var result = new AOOscillator().Calculate(klines);

        result.Value.Should().Be(expected);
    }

    /// <summary>
    /// Verifies lookback and zero-range handling for Ultimate Oscillator.
    /// </summary>
    [Fact]
    public void UltimateOscillator_ShouldRequireTwentyNineCandlesAndHandleFlatMarket()
    {
        var oscillator = new UltimateOscillator();
        var insufficientData = CreateKlines(Enumerable.Repeat(100m, 28));
        var sufficientData = CreateKlines(Enumerable.Repeat(100m, 29));

        Action insufficientCalculation = () => oscillator.Calculate(insufficientData);
        var result = oscillator.Calculate(sufficientData);

        insufficientCalculation.Should().Throw<InvalidOperationException>();
        result.Value.Should().Be(0m);
    }

    /// <summary>
    /// Verifies that ADX remains finite and neutral when the market has no range.
    /// </summary>
    [Fact]
    public void Adx_WithFlatMarket_ShouldReturnZero()
    {
        var result = new AdxOscillator().Calculate(CreateKlines(Enumerable.Repeat(100m, 50)));

        result.Value.Should().Be(0m);
        result.Action.Should().Be(TradeAction.Neutral);
    }

    /// <summary>
    /// Verifies inclusive boundaries used to classify aggregate indicator values.
    /// </summary>
    [Theory]
    [InlineData(-50d, TradeAction.ActiveSell)]
    [InlineData(-15d, TradeAction.Neutral)]
    [InlineData(15d, TradeAction.Neutral)]
    [InlineData(50d, TradeAction.ActiveBuy)]
    public void TechnicalAnalysisResult_ShouldClassifyBoundaryValues(double value, string expectedAction)
    {
        var result = new TechnicalAnalysisResult(new(), new(), value, value, value);

        result.GeneralResultText.Should().Be(expectedAction);
    }

    private static List<BinanceSpotKline> CreateKlines(IEnumerable<decimal> prices)
    {
        return prices.Select((price, index) => new BinanceSpotKline
        {
            OpenTime = DateTime.UnixEpoch.AddMinutes(index),
            OpenPrice = price,
            HighPrice = price,
            LowPrice = price,
            ClosePrice = price,
            Volume = 1m
        }).ToList();
    }

    private static decimal CalculateMacdHistogram(
        IReadOnlyList<decimal> prices,
        int fastPeriod,
        int slowPeriod,
        int signalPeriod)
    {
        var fastEma = CalculateEmaSeries(prices, fastPeriod);
        var slowEma = CalculateEmaSeries(prices, slowPeriod);
        var macd = fastEma
            .Skip(slowPeriod - fastPeriod)
            .Zip(slowEma, (fast, slow) => fast - slow)
            .ToList();
        var signal = CalculateEmaSeries(macd, signalPeriod);

        return macd[^1] - signal[^1];
    }

    private static List<decimal> CalculateEmaSeries(IReadOnlyList<decimal> values, int period)
    {
        decimal multiplier = 2m / (period + 1);
        var result = new List<decimal> { values.Take(period).Average() };

        for (int index = period; index < values.Count; index++)
            result.Add((values[index] - result[^1]) * multiplier + result[^1]);

        return result;
    }
}
