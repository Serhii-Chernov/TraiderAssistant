using Microsoft.ML.Data;

namespace TechnicalAnalysis.Domain.MLModels
{
    public class OptimizationInput
    {
        [LoadColumn(0)] public string Timeframe { get; set; }
        [LoadColumn(1)] public string Asset { get; set; } // <--- ДОБАВИЛИ МОНЕТУ

        // Осцилятори (11)
        [LoadColumn(2)] public float RsiWeight { get; set; }
        [LoadColumn(3)] public float StochasticKWeight { get; set; }
        [LoadColumn(4)] public float CciWeight { get; set; }
        [LoadColumn(5)] public float AdxWeight { get; set; }
        [LoadColumn(6)] public float AoWeight { get; set; }
        [LoadColumn(7)] public float MomentumWeight { get; set; }
        [LoadColumn(8)] public float MacdWeight { get; set; }
        [LoadColumn(9)] public float UltimateWeight { get; set; }
        [LoadColumn(10)] public float WilliamsRWeight { get; set; }
        [LoadColumn(11)] public float BullBearWeight { get; set; }
        [LoadColumn(12)] public float StochasticRsiWeight { get; set; }

        // Ковзні середні (13)
        [LoadColumn(13)] public float Sma10Weight { get; set; }
        [LoadColumn(14)] public float Ema10Weight { get; set; }
        [LoadColumn(15)] public float Sma20Weight { get; set; }
        [LoadColumn(16)] public float Ema20Weight { get; set; }
        [LoadColumn(17)] public float Sma30Weight { get; set; }
        [LoadColumn(18)] public float Ema30Weight { get; set; }
        [LoadColumn(19)] public float Sma50Weight { get; set; }
        [LoadColumn(20)] public float Ema50Weight { get; set; }
        [LoadColumn(21)] public float Sma100Weight { get; set; }
        [LoadColumn(22)] public float Ema100Weight { get; set; }
        [LoadColumn(23)] public float Sma200Weight { get; set; }
        [LoadColumn(24)] public float Ema200Weight { get; set; }
        [LoadColumn(25)] public float Hma9Weight { get; set; }

        // Налаштування ризик-менеджменту (4)
        [LoadColumn(26)] public float BuyThreshold { get; set; }
        [LoadColumn(27)] public float SellThreshold { get; set; }
        [LoadColumn(28)] public float StopLoss { get; set; }
        [LoadColumn(29)] public float TakeProfit { get; set; }

        // === НОВЫЕ ПАРАМЕТРЫ (Веса групп) ===
        [LoadColumn(30)] public float OscillatorGroupWeight { get; set; }
        [LoadColumn(31)] public float MaGroupWeight { get; set; }

        // Risk-adjusted optimization target. Raw profit is deliberately not used as the label.
        [LoadColumn(32), ColumnName("Label")] public float RiskAdjustedScore { get; set; }

        // Walk-forward targets are normalized to a one-year horizon. They are training metadata,
        // not strategy parameters, so they must not leak into API responses or feature columns.
        [ColumnName("AnnualReturnLabel")]
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public float AnnualReturnTarget { get; set; }

        [ColumnName("MaxDrawdownLabel")]
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public float MaxDrawdownTarget { get; set; }

        [ColumnName("ProfitFactorLabel")]
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public float ProfitFactorTarget { get; set; }

        [ColumnName("AnnualTradesLabel")]
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public float AnnualTradesTarget { get; set; }

        public OptimizationInput CopyWithWalkForwardTargets(
            float score,
            float annualReturn,
            float maxDrawdown,
            float profitFactor,
            float annualTrades)
        {
            var copy = (OptimizationInput)MemberwiseClone();
            copy.RiskAdjustedScore = score;
            copy.AnnualReturnTarget = annualReturn;
            copy.MaxDrawdownTarget = maxDrawdown;
            copy.ProfitFactorTarget = profitFactor;
            copy.AnnualTradesTarget = annualTrades;
            return copy;
        }
    }
}