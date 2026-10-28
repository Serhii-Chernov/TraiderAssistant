using TechnicalAnalysis.Domain.MLModels;

namespace TechnicalAnalysis.Domain.Models;

public class PredictedStrategyCandidate
{
    public string Asset { get; set; }
    public string Timeframe { get; set; }
    public float PredictedScore { get; set; }
    public OptimizationInput Parameters { get; set; }
}

public class StrategyCandidatePreview
{
    public int SearchCombinationsPerStrategy { get; set; }
    public int CandidatesPerStrategy { get; set; }
    public List<PredictedStrategyCandidate> Candidates { get; set; } = new();
}

public class StrategyValidationReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialDeposit { get; set; }
    public int CandidatesPerStrategy { get; set; }
    public int ActiveStrategiesCreated { get; set; }
    public List<StrategyGroupValidationResult> Strategies { get; set; } = new();
}

public class StrategyGroupValidationResult
{
    public string Asset { get; set; }
    public string Timeframe { get; set; }
    public int CandidatesEvaluated { get; set; }
    public bool HasAcceptedStrategy { get; set; }
    public StrategyCandidateValidationResult SelectedStrategy { get; set; }
    public List<StrategyCandidateValidationResult> Candidates { get; set; } = new();
}

public class StrategyCandidateValidationResult
{
    public int Rank { get; set; }
    public float PredictedScore { get; set; }
    public decimal ActualScore { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public int PositivePeriods { get; set; }
    public int RequiredPositivePeriods { get; set; }
    public decimal BestPeriodProfitShare { get; set; }
    public bool IsAccepted { get; set; }
    public List<string> RejectionReasons { get; set; } = new();
    public OptimizationInput Parameters { get; set; }
    public List<StrategyPeriodValidationResult> Periods { get; set; } = new();
}

public class StrategyPeriodValidationResult
{
    public int Year { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
}
