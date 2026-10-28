using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Shared
{
    public class TradeRecord
    {
        public DateTime EntryTime { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal? ExitPrice { get; set; }
        public string Type { get; set; } // "Long" (пока только покупка)
        public decimal Profit { get; set; }
        public decimal ProfitPercent { get; set; }
        public string EntryIndicatorsJson { get; set; }
    }
}
