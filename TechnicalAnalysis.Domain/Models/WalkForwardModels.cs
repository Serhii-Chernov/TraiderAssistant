using TechnicalAnalysis.Domain.MLModels;

namespace TechnicalAnalysis.Domain.Models;

public sealed class WalkForwardAnalysisReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialDeposit { get; set; }
    public int BaseCandidatesPerStrategy { get; set; }
    public int SearchCandidatesPerFold { get; set; }
    public string CandidateSetVersion { get; set; }
    public bool StrategiesActivated { get; set; }
    public int ActiveStrategiesCreated { get; set; }
    public List<WalkForwardStrategyResult> Strategies { get; set; } = new();
}

public sealed class WalkForwardStrategyResult
{
    public string Asset { get; set; }
    public string Timeframe { get; set; }
    public bool IsAccepted { get; set; }
    public List<string> RejectionReasons { get; set; } = new();
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public int PositiveFolds { get; set; }
    public int RequiredPositiveFolds { get; set; }
    public decimal BestFoldProfitShare { get; set; }
    public decimal WalkForwardScore { get; set; }
    public OptimizationInput FinalCandidate { get; set; }
    public float FinalPredictedScore { get; set; }
    public List<WalkForwardFoldResult> Folds { get; set; } = new();
}

public sealed class WalkForwardFoldResult
{
    public int TestYear { get; set; }
    public DateTime TrainingStart { get; set; }
    public DateTime TrainingEnd { get; set; }
    public DateTime TestStart { get; set; }
    public DateTime TestEnd { get; set; }
    public bool IsPartialPeriod { get; set; }
    public float PredictedScore { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public OptimizationInput Parameters { get; set; }
}

internal sealed record WalkForwardPeriod(int Year, DateTime Start, DateTime End, bool IsPartial);

internal sealed class CandidatePeriodBacktest
{
    public int CandidateIndex { get; set; }
    public OptimizationInput Parameters { get; set; }
    public WalkForwardPeriod Period { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
}