using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    internal class WilliamsROscillator : IOscillator
    {
        public string Name { get; set; } = "Williams%R";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 14; // Период для расчёта Williams %R
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();

            // Индексы для поиска максимума и минимума за период
            int startIndex = closePrices.Count() - period;

            // Находим максимальное значение High и минимальное значение Low за период
            decimal highestHigh = highs.Skip(startIndex).Take(period).Max();
            decimal lowestLow = lows.Skip(startIndex).Take(period).Min();

            // Цена закрытия текущего дня
            decimal currentClose = closePrices.Last();

            // Рассчитываем Williams %R
            decimal williamsR = ((highestHigh - currentClose) / (highestHigh - lowestLow)) * -100;

            return williamsR;
            
        }
    }
}
