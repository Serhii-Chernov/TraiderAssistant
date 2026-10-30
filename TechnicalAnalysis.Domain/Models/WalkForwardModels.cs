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
    public string SelectorVersion { get; set; }
    public string AssetFilter { get; set; }
    public string TimeframeFilter { get; set; }
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
    public WalkForwardRiskPrediction FinalPrediction { get; set; }
    public HistoricalDataQualityResult DataQuality { get; set; }
    public WalkForwardBenchmarkSummary Baseline { get; set; }
    public List<WalkForwardFoldResult> Folds { get; set; } = new();
}

public sealed class WalkForwardBenchmarkSummary
{
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal WalkForwardScore { get; set; }
    public decimal MlReturnDelta { get; set; }
    public decimal MlScoreDelta { get; set; }
    public bool MlOutperformedBaseline { get; set; }
}

public sealed class HistoricalDataQualityResult
{
    public string Asset { get; set; }
    public string Timeframe { get; set; }
    public int ActualCandles { get; set; }
    public int ExpectedCandles { get; set; }
    public int MissingCandles { get; set; }
    public decimal MissingPercent { get; set; }
    public int MaximumGapCandles { get; set; }
    public List<HistoricalDataGap> Gaps { get; set; } = new();
}

public sealed class HistoricalDataGap
{
    public DateTime ExpectedOpenTime { get; set; }
    public DateTime ReceivedOpenTime { get; set; }
    public int MissingCandles { get; set; }
}

internal sealed class HistoricalDataBatch
{
    public List<Binance.Net.Objects.Models.Spot.BinanceSpotKline> Candles { get; set; } = new();
    public HistoricalDataQualityResult Quality { get; set; }
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
    public WalkForwardRiskPrediction Prediction { get; set; }
    public WalkForwardPredictionError PredictionError { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public OptimizationInput Parameters { get; set; }
    public WalkForwardFoldBenchmark Baseline { get; set; }
}

public sealed class WalkForwardFoldBenchmark
{
    public int CandidateIndex { get; set; }
    public decimal TrainingScore { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal SharpeRatio { get; set; }
    public OptimizationInput Parameters { get; set; }
}

public sealed class WalkForwardRiskPrediction
{
    public decimal PredictedAnnualReturn { get; set; }
    public decimal PredictedMaxDrawdown { get; set; }
    public decimal? PredictedProfitFactor { get; set; }
    public decimal PredictedAnnualTrades { get; set; }
    public decimal ConservativeScore { get; set; }
    public bool RiskFilterPassed { get; set; }
    public bool UsedFallback { get; set; }
    public int CandidatesEvaluated { get; set; }
    public int CandidatesPassedRiskFilter { get; set; }
    public List<string> RejectionReasons { get; set; } = new();
}

public sealed class WalkForwardPredictionError
{
    public decimal ActualAnnualizedReturn { get; set; }
    public decimal ActualMaxDrawdown { get; set; }
    public decimal? ActualProfitFactor { get; set; }
    public decimal ActualAnnualizedTrades { get; set; }
    public decimal AnnualReturnAbsoluteError { get; set; }
    public decimal MaxDrawdownAbsoluteError { get; set; }
    public decimal? ProfitFactorAbsoluteError { get; set; }
    public decimal AnnualTradesAbsoluteError { get; set; }
}

internal sealed record WalkForwardPeriod(int Year, DateTime Start, DateTime End, bool IsPartial);

internal sealed record WalkForwardTrainingTargets(
    decimal Score,
    decimal AnnualReturn,
    decimal MaxDrawdown,
    decimal ProfitFactor,
    decimal AnnualTrades);

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
    public HistoricalDataQualityResult DataQuality { get; set; }
}

internal sealed class WalkForwardModelBundle
{
    public Microsoft.ML.ITransformer AnnualReturnModel { get; init; }
    public Microsoft.ML.ITransformer MaxDrawdownModel { get; init; }
    public Microsoft.ML.ITransformer ProfitFactorModel { get; init; }
    public Microsoft.ML.ITransformer AnnualTradesModel { get; init; }
}

internal sealed class WalkForwardCandidatePrediction
{
    public string Asset { get; init; }
    public string Timeframe { get; init; }
    public float PredictedScore { get; init; }
    public OptimizationInput Parameters { get; init; }
    public WalkForwardRiskPrediction RiskPrediction { get; init; }
}