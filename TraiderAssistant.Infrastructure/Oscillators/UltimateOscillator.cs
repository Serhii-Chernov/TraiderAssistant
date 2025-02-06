using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    internal class UltimateOscillator : IOscillator
    {
        public string Name { get; set; } = "Ultimate Oscillator";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period1 = 7;
            int period2 = 14;
            int period3 = 28;

            var closePrices = data.Select(x => x.ClosePrice).ToList();
            var highs = data.Select(x => x.HighPrice).ToList();
            var lows = data.Select(x => x.LowPrice).ToList();

            if (closePrices.Count() < period3)
                throw new InvalidOperationException("Недостаточно данных для расчёта Ultimate Oscillator.");

            // Вычисляем BP и TR для каждого дня
            List<decimal> bpList = new List<decimal>();
            List<decimal> trList = new List<decimal>();

            for (int i = 1; i < closePrices.Count(); i++)
            {

                decimal close = closePrices.ToList()[i];
                decimal prevClose = closePrices.ToList()[i - 1];
                decimal low = lows.ToList()[i];
                decimal high = highs.ToList()[i];

                decimal bp = close - Math.Min(low, prevClose);
                decimal tr = Math.Max(high, prevClose) - Math.Min(low, prevClose);

                bpList.Add(bp);
                trList.Add(tr);
            }

            // Вычисляем средние BP и TR для трёх периодов
            decimal avgBP1 = bpList.TakeLast(period1).Sum();
            decimal avgTR1 = trList.TakeLast(period1).Sum();

            decimal avgBP2 = bpList.TakeLast(period2).Sum();
            decimal avgTR2 = trList.TakeLast(period2).Sum();

            decimal avgBP3 = bpList.TakeLast(period3).Sum();
            decimal avgTR3 = trList.TakeLast(period3).Sum();

            // Вычисляем Ultimate Oscillator
            decimal uo = 100 * ((4 * (avgBP1 / avgTR1) + 2 * (avgBP2 / avgTR2) + 1 * (avgBP3 / avgTR3)) / (4 + 2 + 1));

            return uo;
        }
    }
}
