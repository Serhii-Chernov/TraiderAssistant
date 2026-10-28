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

        public List<TradeLog> Trades { get; set; } = new List<TradeLog>();
    }
}
