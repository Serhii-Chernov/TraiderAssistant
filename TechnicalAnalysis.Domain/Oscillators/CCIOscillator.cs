using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Domain
{
    public class CCIOscillator: IOscillator
    {
        public string Name { get; set; } = "CCI";

        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 20;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();
            var typicalPrices = highs.Zip(lows, (h, l) => (h, l))
                                     .Zip(closePrices, (hl, c) => (hl.h + hl.l + c) / 3)
                                     .ToList();
            decimal sma = typicalPrices.Skip(typicalPrices.Count - period).Take(period).Average();
            decimal meanDeviation = typicalPrices.Skip(typicalPrices.Count - period).Take(period)
                                                 .Average(tp => Math.Abs(tp - sma));

            return (typicalPrices.Last() - sma) / (0.015m * meanDeviation);
        }
    }
}
