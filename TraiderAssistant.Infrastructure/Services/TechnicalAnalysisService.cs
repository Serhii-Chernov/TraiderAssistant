using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Services
{
    public class TechnicalAnalysisService
    {
        private IEnumerable<decimal> closePrices;
        private IEnumerable<decimal> highs;
        private IEnumerable<decimal> lows;
        private int period;

        //????rename to PerformTechnicalAnalysis
        public Dictionary<string, decimal> GetTechnicalAnalysis(IEnumerable<decimal> closePrices, IEnumerable<decimal> highs, IEnumerable<decimal> lows, int period)
        {
            this.closePrices = closePrices;
            this.highs = highs;
            this.lows = lows;
            this.period = period;

            var result = new Dictionary<string, decimal>();
            
            result.Add("RSI", CalculateRSI());
            result.Add("StochasticK", CalculateStochasticK());
            result.Add("CCI", CalculateCCI());
            result.Add("ADX", CalculateADX());
            result.Add("AO", CalculateAO());
            result.Add("Momentum", CalculateMomentum());
            return result;
        }

        /// <summary>
        /// Относительная сила (RSI)
        /// </summary>
        /// <returns>
        /// Показатель перекупленности/перепроданности.
        /// Значение выше 70 указывает на перекупленность, ниже 30 — на перепроданность.
        /// Рассчитывается на основе соотношения средних приростов к средним потерям за определённый период.
        /// </returns>
        private decimal CalculateRSI()
        {
            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < closePrices.Count(); i++)
            {
                var change = closePrices.ElementAt(i) - closePrices.ElementAt(i - 1);
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(Math.Abs(change));
            }

            var avgGain = gains.TakeLast(period).Average();
            var avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0) return 100;
            var rs = avgGain / avgLoss;

            return 100 - (100 / (1 + rs));
        }

        /// <summary>
        /// Стохастический осциллятор
        /// </summary>
        /// <returns>
        /// Показывает, где текущая цена находится относительно диапазона за заданный период.
        /// Значения выше 80 указывают на перекупленность, ниже 20 — на перепроданность.
        /// </returns>
        private decimal CalculateStochasticK()
        {
            int startIndex = closePrices.Count() - period;
            decimal highestHigh = highs.Skip(startIndex).Take(period).Max();
            decimal lowestLow = lows.Skip(startIndex).Take(period).Min();
            decimal currentClose = closePrices.Last();

            return ((currentClose - lowestLow) / (highestHigh - lowestLow)) * 100;
        }

        /// <summary>
        /// Индекс товарного канала (CCI)
        /// </summary>
        /// <returns>Измеряет отклонение текущей цены от её среднего значения.</returns>
        private decimal CalculateCCI()
        {
            var typicalPrices = highs.Zip(lows, (h, l) => (h, l))
                                     .Zip(closePrices, (hl, c) => (hl.h + hl.l + c) / 3)
                                     .ToList();
            decimal sma = typicalPrices.Skip(typicalPrices.Count - period).Take(period).Average();
            decimal meanDeviation = typicalPrices.Skip(typicalPrices.Count - period).Take(period)
                                                 .Average(tp => Math.Abs(tp - sma));

            return (typicalPrices.Last() - sma) / (0.015m * meanDeviation);
        }

        /// <summary>
        /// Индекс силы тренда (ADX)
        /// </summary>
        /// <returns></returns>
        private decimal CalculateADX()
        {
            int n = highs.Count();
            List<decimal> tr = new List<decimal>();
            List<decimal> plusDM = new List<decimal>();
            List<decimal> minusDM = new List<decimal>();

            for (int i = 1; i < n; i++)
            {
                decimal currentHigh = highs.ToList()[i], previousHigh = highs.ToList()[i - 1];
                decimal currentLow = lows.ToList()[i], previousLow = lows.ToList()[i - 1];
                decimal currentClose = closePrices.ToList()[i], previousClose = closePrices.ToList()[i - 1];

                // True Range (TR)
                tr.Add(Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - previousClose), Math.Abs(currentLow - previousClose))));

                // +DM and -DM
                decimal upMove = currentHigh - previousHigh;
                decimal downMove = previousLow - currentLow;
                plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
                minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);
            }

            // Smooth TR, +DM, and -DM
            decimal smoothTR = tr.Take(period).Sum();
            decimal smoothPlusDM = plusDM.Take(period).Sum();
            decimal smoothMinusDM = minusDM.Take(period).Sum();

            decimal adx = 0;
            for (int i = period; i < tr.Count; i++)
            {
                smoothTR = smoothTR - (smoothTR / period) + tr[i];
                smoothPlusDM = smoothPlusDM - (smoothPlusDM / period) + plusDM[i];
                smoothMinusDM = smoothMinusDM - (smoothMinusDM / period) + minusDM[i];

                decimal plusDI = (smoothPlusDM / smoothTR) * 100;
                decimal minusDI = (smoothMinusDM / smoothTR) * 100;
                decimal dx = (Math.Abs(plusDI - minusDI) / (plusDI + minusDI)) * 100;

                if (i == period)
                {
                    adx = dx;
                }
                else
                {
                    adx = ((adx * (period - 1)) + dx) / period;
                }
            }

            return adx;
        }


        /// <summary>
        /// Чудесный осциллятор (Awesome Oscillator)
        /// </summary>
        /// <returns>Средняя разница между медленной и быстрой скользящими средними.</returns>
        private decimal CalculateAO()
        {
            decimal sma5 = closePrices.Skip(closePrices.Count() - 5).Average();
            decimal sma34 = closePrices.Skip(closePrices.Count() - 34).Average();

            return sma5 - sma34;
        }

        /// <summary>
        /// Индикатор импульса (Momentum)
        /// </summary>
        /// <returns>Измеряет скорость изменения цены.</returns>
        private decimal CalculateMomentum()
        {
            return closePrices.Last() - closePrices.ToList()[closePrices.Count() - period - 1];//проверить наскольео правильно
            //return closePrices.Last() - closePrices[closePrices.Count() - period - 1];
        }

        private decimal CalculateEMA()
        {
            decimal multiplier = 2 / (period + 1);
            decimal ema = closePrices.Take(period).Average(); // Начальная EMA как SMA

            for (int i = period; i < closePrices.Count(); i++)
            {
                ema = (closePrices.ToList()[i] - ema) * multiplier + ema;
            }

            return ema;
        }

        public static (double macd, double signal, double histogram) CalculateMACD(List<double> prices, int shortPeriod = 12, int longPeriod = 26, int signalPeriod = 9)
        {
            decimal emaShort = CalculateEMA(prices, shortPeriod);
            decimal emaLong = CalculateEMA(prices, longPeriod);
            double macd = emaShort - emaLong;

            // Рассчитать сигнальную линию как EMA от MACD
            List<double> macdLine = prices.Skip(longPeriod - 1).Select((_, i) => emaShort - emaLong).ToList();
            double signal = CalculateEMA(macdLine, signalPeriod);

            double histogram = macd - signal;
            return (macd, signal, histogram);
        }

    }
}
