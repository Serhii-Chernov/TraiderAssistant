using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    internal class StochasticRSIOscillator : IOscillator
    {
        public string Name { get; set; } = "StochasticRSI";

        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            int period = 14;
            // Проверяем, достаточно ли данных для расчета
            if (closePrices.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчета Stochastic RSI");

            List<decimal> rsiValues = new List<decimal>();

            // Рассчитываем RSI для каждого окна
            for (int i = period; i <= closePrices.Count(); i++)
            {
                // Берем последние "period" значений для расчета RSI
                var window = closePrices.Skip(i - period).Take(period).ToList();
                rsiValues.Add(CalculateRSI(window)); // Теперь вызываем CalculateRSI для каждого окна
            }

            // Находим максимальное и минимальное значение RSI в пределах последнего периода
            decimal highestRSI = rsiValues.TakeLast(period).Max();
            decimal lowestRSI = rsiValues.TakeLast(period).Min();
            decimal currentRSI = rsiValues.Last();

            // Защита от деления на ноль
            decimal denominator = highestRSI - lowestRSI;
            if (denominator == 0)
                return 0; // Если диапазон RSI = 0, Stochastic RSI тоже 0

            return ((currentRSI - lowestRSI) / denominator) * 100;
        }

        private decimal CalculateRSI(List<decimal> prices, int period = 14)
        {
            
            if (prices.Count < period)
                throw new InvalidOperationException("Недостаточно данных для расчета RSI");

            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < period; i++)
            {
                var change = prices[i] - prices[i - 1];
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(Math.Abs(change));
            }

            var avgGain = gains.Any() ? gains.Average() : 0;
            var avgLoss = losses.Any() ? losses.Average() : 0;

            if (avgLoss == 0) return 100; // Если потери равны нулю, RSI = 100
            var rs = avgGain / avgLoss;

            return 100 - (100 / (1 + rs));
        }
    }
}
