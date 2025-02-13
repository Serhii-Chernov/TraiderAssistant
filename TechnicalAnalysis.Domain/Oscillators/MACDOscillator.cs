using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechnicalAnalysis.Domain
{
    internal class MACDOscillator : IOscillator
    {
        public string Name { get; set; } = "MACD";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int shortPeriod = 12;
            int longPeriod = 26;
            int signalPeriod = 9;


            var closePrices = data.Select(k => k.ClosePrice).ToList();
            if (closePrices.Count() < longPeriod)
                throw new InvalidOperationException("Недостаточно данных для расчета MACD");

            List<decimal> macdValues = new List<decimal>();

            // Рассчитываем MACD для каждого доступного значения цены
            for (int i = longPeriod - 1; i < closePrices.Count(); i++)
            {
                decimal shortEma = CalculateEMA(closePrices.Take(i + 1),shortPeriod );
                decimal longEma = CalculateEMA(closePrices.Take(i + 1), longPeriod);
                macdValues.Add(shortEma - longEma);
            }

            // Проверка наличия достаточного количества значений для сигнальной линии
            decimal signal;
            if (macdValues.Count >= signalPeriod)
            {
                signal = CalculateEMA(macdValues, signalPeriod);
            }
            else
            {
                signal = macdValues.Last(); // Берем последнее значение MACD, если данных мало
            }

            decimal histogram = macdValues.Last() - signal;

            return histogram;

            //int shortPeriod = 12;
            //int longPeriod = 26;
            //int signalPeriod = 9;
            //var closePrices = data.Select(k => k.ClosePrice).ToList();
            //var shortEMA = CalculateEMA(closePrices, shortPeriod);
            //var longEMA = CalculateEMA(closePrices, longPeriod);
            //var macdLine = shortEMA.Zip(longEMA, (shortEma, longEma) => shortEma - longEma).ToList();
            //var signalLine = CalculateEMA(macdLine, signalPeriod);
            //return macdLine.Last() - signalLine.Last();
        }
        /// <summary>
        /// Рассчитывает экспоненциальную скользящую среднюю (EMA).
        /// </summary>
        
        private decimal CalculateEMA( IEnumerable<decimal> data, int period)
        {
            if (data.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчёта EMA.");

            decimal multiplier = 2m / (period + 1);
            decimal ema = data.Take(period).Average(); // Начальное значение — SMA

            foreach (var price in data.Skip(period))
            {
                ema = (price - ema) * multiplier + ema;
            }

            return ema;
        }

    }
}
