using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net.Objects.Models.Spot;

namespace TechnicalAnalysis.Domain
{
    public class MomentumOscillator : IOscillator
    {
        public string Name { get; set; } = "Momentum";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 10;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            return closePrices.Last() - closePrices.ToList()[closePrices.Count() - period - 1];

            //int period = 14;
            //var closePrices = data.Select(k => k.ClosePrice).ToList();
            //List<decimal> momentum = new List<decimal>();
            //for (int i = period; i < closePrices.Count; i++)
            //{
            //    decimal currentPrice = closePrices.ToList()[i];
            //    decimal previousPrice = closePrices.ToList()[i - period];
            //    momentum.Add(currentPrice - previousPrice);
            //}
            //decimal sum = momentum.Sum();
            //decimal average = sum / momentum.Count;
            //return average;
        }
    }
}
