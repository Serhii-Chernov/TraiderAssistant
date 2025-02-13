using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Domain
{
    
    public class AOOscillator : IOscillator
    {
        public string Name { get; set; } = "AO";

        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            decimal sma5 = closePrices.Skip(closePrices.Count() - 5).Average();
            decimal sma34 = closePrices.Skip(closePrices.Count() - 34).Average();

            return sma5 - sma34;
        }
    }
}
