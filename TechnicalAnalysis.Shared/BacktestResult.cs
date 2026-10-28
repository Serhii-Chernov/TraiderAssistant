using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Shared
{
    public class BacktestResult
    {
        public decimal InitialBalance { get; set; }
        public decimal FinalBalance { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal ProfitPercentage { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal MaxDrawdown { get; set; }
        public List<TradeRecord> Trades { get; set; } = new List<TradeRecord>();

        public decimal ProfitFactor { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossLoss { get; set; }
    }
}