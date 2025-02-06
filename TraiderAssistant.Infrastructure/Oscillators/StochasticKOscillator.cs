using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    public class StochasticKOscillator : IOscillator
    {
        public string Name
        {
            get; set;
        } = "StochasticK";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();
            int startIndex = closePrices.Count() - period;
            decimal highestHigh = highs.Skip(startIndex).Take(period).Max();
            decimal lowestLow = lows.Skip(startIndex).Take(period).Min();
            decimal currentClose = closePrices.Last();

            return ((currentClose - lowestLow) / (highestHigh - lowestLow)) * 100;
        }
    }
}
