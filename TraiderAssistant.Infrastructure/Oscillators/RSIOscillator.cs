using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    public class RSIOscillator: IOscillator
    {
        public string Name { get; set; } = "RSI";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            if (closePrices.Count < period)
                throw new ArgumentException("Not enough data to calculate.");
            decimal sumGain = 0;
            decimal sumLoss = 0;
            for (int i = 1; i < period; i++)
            {
                decimal delta = closePrices[i] - closePrices[i - 1];
                if (delta > 0)
                    sumGain += delta;
                else
                    sumLoss -= delta;
            }
            decimal avgGain = sumGain / period;
            decimal avgLoss = sumLoss / period;
            if (avgLoss == 0)
                return 100;
            if (avgGain == 0)
                return 0;
            decimal rs = avgGain / avgLoss;
            return 100 - 100 / (1 + rs);
        }
    }
}
