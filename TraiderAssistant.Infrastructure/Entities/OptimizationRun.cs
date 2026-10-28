using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Entities
{
    public class OptimizationRun
    {
        public int Id { get; set; }
        public DateTime RunDate { get; set; } = DateTime.UtcNow;

        // Параметры прогона
        public string Timeframe { get; set; } // "OneMinute", "OneHour"...
        public string Asset { get; set; } // "BTCUSDT"

        // Веса индикаторов (сериализуем в JSON, чтобы было гибко)
        public string IndicatorWeightsJson { get; set; }

        // Результаты
        public decimal TotalProfitPercent { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossLoss { get; set; }

        // Reproducibility metadata. Legacy rows keep these fields null.
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public string CandidateSetVersion { get; set; }
        public int? CandidateIndex { get; set; }
        public decimal TradingFee { get; set; }
        public decimal Slippage { get; set; }
        public string EngineVersion { get; set; }

        public List<TradeLog> Trades { get; set; } = new List<TradeLog>();
    }
}