namespace TechnicalAnalysis.Domain.Models;

/// <summary>
/// Represents monthly out-of-sample performance grouped by asset and timeframe.
/// </summary>
public class ForwardTestMonthlyResult
{
    public string Month { get; set; }
    public string CurrencyPair { get; set; }
    public string Timeframe { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal NetProfit { get; set; }
}
