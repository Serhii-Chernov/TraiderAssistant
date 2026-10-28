using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Entities
{
    public class TradeLog
    {
        public int Id { get; set; }
        public int OptimizationRunId { get; set; }
        public OptimizationRun OptimizationRun { get; set; }

        public string TradeType { get; set; } // "Long"
        public DateTime EntryTime { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal ProfitPercent { get; set; }

        // Самое важное для ML: значения индикаторов В МОМЕНТ ВХОДА
        // Мы сохраним их как JSON, чтобы ML мог искать паттерны
        // Пример: { "RSI": 25.5, "MACD": -100, "SMA200_Dist": -2.5% }
        public string EntryIndicatorsJson { get; set; }
    }
}
