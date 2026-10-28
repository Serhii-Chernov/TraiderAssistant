using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Domain.MLModels
{
    public class WeightsJsonSchema
    {
        public decimal OscillatorGroupWeight { get; set; }
        public decimal MaGroupWeight { get; set; }
        public Dictionary<string, decimal> Oscillators { get; set; }
        public Dictionary<string, decimal> MAs { get; set; }
        public int BuyThreshold { get; set; }
        public int SellThreshold { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
    }
}
