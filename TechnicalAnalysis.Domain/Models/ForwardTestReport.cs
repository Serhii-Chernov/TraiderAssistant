namespace TechnicalAnalysis.Domain.Models;

/// <summary>
/// Contains both chart-ready monthly rows and exact whole-period strategy metrics.
/// </summary>
public class ForwardTestReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialDeposit { get; set; }
    public string StrategyMode { get; set; }
    public List<ForwardTestStrategyResult> Strategies { get; set; } = new();
    public List<ForwardTestMonthlyResult> Months { get; set; } = new();
}

/// <summary>
/// Exact metrics for one asset/timeframe over the complete forward-test interval.
/// </summary>
public class ForwardTestStrategyResult
{
    public string CurrencyPair { get; set; }
    public string Timeframe { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public int MaxConsecutiveLosses { get; set; }
    public int PositiveMonths { get; set; }
    public int TotalMonths { get; set; }
    public decimal PositiveMonthRate { get; set; }
    public decimal BestMonthProfit { get; set; }
    public decimal BestMonthProfitShare { get; set; }
    public bool IsStable { get; set; }
    public string StabilityReason { get; set; }
}