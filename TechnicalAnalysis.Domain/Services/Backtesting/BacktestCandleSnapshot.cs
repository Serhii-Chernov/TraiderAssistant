namespace TechnicalAnalysis.Domain.Services.Backtesting;

/// <summary>
/// Contains one historical candle and the technical-analysis actions known after it closes.
/// </summary>
internal sealed class BacktestCandleSnapshot
{
    public DateTime OpenTime { get; init; }
    public DateTime CloseTime { get; init; }
    public decimal OpenPrice { get; init; }
    public decimal HighPrice { get; init; }
    public decimal LowPrice { get; init; }
    public decimal ClosePrice { get; init; }
    public decimal[] OscillatorActions { get; set; } = Array.Empty<decimal>();
    public decimal[] MovingAverageActions { get; set; } = Array.Empty<decimal>();
}

/// <summary>
/// Defines strategy and execution parameters used by the deterministic backtest engine.
/// </summary>
internal sealed record BacktestSettings(
    decimal[] OscillatorWeights,
    decimal[] MovingAverageWeights,
    decimal OscillatorGroupWeight,
    decimal MovingAverageGroupWeight,
    int BuyThreshold,
    int SellThreshold,
    decimal StopLoss,
    decimal TakeProfit,
    decimal TradingFee)
{
    /// <summary>
    /// Adverse execution slippage applied to every entry and exit fill.
    /// </summary>
    public decimal Slippage { get; init; }
}