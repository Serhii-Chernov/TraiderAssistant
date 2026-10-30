namespace TechnicalAnalysis.Domain.Models;

public sealed class TournamentSignalResult
{
    public string Asset { get; set; }
    public string Market { get; set; } = "Spot";
    public string Action { get; set; }
    public int Confidence { get; set; }
    public string SuggestedOrderType { get; set; }
    public decimal? ReferencePrice { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public decimal MaximumAccountRiskPercent { get; set; } = 0.5m;
    public string PositionSizeFormula { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime DataAsOf { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsStale { get; set; }
    public bool CanOpenNewSpotPosition { get; set; }
    public string Warning { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<TournamentTimeframeSnapshot> Timeframes { get; set; } = new();
}

public sealed class TournamentTimeframeSnapshot
{
    public string Timeframe { get; set; }
    public DateTime LastClosedCandleTime { get; set; }
    public decimal LastPrice { get; set; }
    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Rsi14 { get; set; }
    public decimal Atr14 { get; set; }
    public decimal AtrPercent { get; set; }
    public decimal VolumeRatio { get; set; }
    public bool BrokeRecentHigh { get; set; }
    public bool BrokeRecentLow { get; set; }
    public int Score { get; set; }
    public string Trend { get; set; }
}
