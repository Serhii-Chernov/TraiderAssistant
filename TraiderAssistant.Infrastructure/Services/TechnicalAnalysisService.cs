using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        //private int period;


        //????rename to PerformTechnicalAnalysis
        public TechnicalAnalysisResult GetTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            closePrices = data.Select(k => k.ClosePrice).ToArray();
            highs = data.Select(k => k.HighPrice).ToArray();
            lows = data.Select(k => k.LowPrice).ToArray();
            //period = 14;//????

            var osciliators = new Dictionary<string, decimal>();

            osciliators.Add("RSI", CalculateRSI());
            osciliators.Add("StochasticK", CalculateStochasticK());
            osciliators.Add("CCI", CalculateCCI());
            osciliators.Add("ADX", CalculateADX());
            osciliators.Add("AO", CalculateAO());
            osciliators.Add("Momentum", CalculateMomentum());
            osciliators.Add("MACD", CalculateMACD());
            osciliators.Add("UltimateOscillator", CalculateUltimateOscillator());
            osciliators.Add("WilliamsR", CalculateWilliamsR());
            osciliators.Add("BullBearPower", CalculateBullBearPower());
            osciliators.Add("StochasticRSI", CalculateStochasticRSI());

            var MAs = CalculateMovingAverages(closePrices);

            TechnicalAnalysisResult technicalAnalysisResult = new TechnicalAnalysisResult(osciliators, MAs);
            return technicalAnalysisResult;
        }
        



        /// <summary>
        /// Относительная сила (RSI)
        /// </summary>
        /// <returns>
        /// Показатель перекупленности/перепроданности.
        /// Значение выше 70 указывает на перекупленность, ниже 30 — на перепроданность.
        /// Рассчитывается на основе соотношения средних приростов к средним потерям за определённый период.
        /// </returns>
        private decimal CalculateRSI(List<decimal> prices = null, int period = 14)
        {
            prices ??= closePrices.ToList();
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


        //private decimal CalculateRSI()
        //{
        //    int period = 14;
        //    var gains = new List<decimal>();
        //    var losses = new List<decimal>();

        //    for (int i = 1; i < closePrices.Count(); i++)
        //    {
        //        var change = closePrices.ElementAt(i) - closePrices.ElementAt(i - 1);
        //        if (change > 0)
        //            gains.Add(change);
        //        else
        //            losses.Add(Math.Abs(change));
        //    }

        //    var avgGain = gains.TakeLast(period).Average();
        //    var avgLoss = losses.TakeLast(period).Average();

        //    if (avgLoss == 0) return 100;
        //    var rs = avgGain / avgLoss;

        //    return 100 - (100 / (1 + rs));
        //}

        /// <summary>
        /// Стохастический осциллятор
        /// </summary>
        /// <returns>
        /// Показывает, где текущая цена находится относительно диапазона за заданный период.
        /// Значения выше 80 указывают на перекупленность, ниже 20 — на перепроданность.
        /// </returns>
        private decimal CalculateStochasticK()
        {
            int period = 14;
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
            int period = 20;
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
            int period = 20;
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
            //int period = 34;
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
            int period = 10;
            return closePrices.Last() - closePrices.ToList()[closePrices.Count() - period - 1];//проверить наскольео правильно
            //return closePrices.Last() - closePrices[closePrices.Count() - period - 1];
        }
        /// <summary>
        /// Рассчитывает MACD (Moving Average Convergence Divergence).
        /// </summary>
        /// <returns>
        /// Кортеж, содержащий три значения:
        /// 1. MACD (разница между быстрой и медленной EMA)
        /// 2. Сигнальная линия (EMA от MACD)
        /// 3. Гистограмма (разница между MACD и сигнальной линией)
        /// </returns>
        private decimal CalculateMACD(int shortPeriod = 12, int longPeriod = 26, int signalPeriod = 9)
        {
            if (closePrices.Count() < longPeriod)
                throw new InvalidOperationException("Недостаточно данных для расчета MACD");

            List<decimal> macdValues = new List<decimal>();

            // Рассчитываем MACD для каждого доступного значения цены
            for (int i = longPeriod - 1; i < closePrices.Count(); i++)
            {
                decimal shortEma = CalculateEMA(shortPeriod, closePrices.Take(i + 1).ToList());
                decimal longEma = CalculateEMA(longPeriod, closePrices.Take(i + 1).ToList());
                macdValues.Add(shortEma - longEma);
            }

            // Проверка наличия достаточного количества значений для сигнальной линии
            decimal signal;
            if (macdValues.Count >= signalPeriod)
            {
                signal = CalculateEMA(signalPeriod, macdValues);
            }
            else
            {
                signal = macdValues.Last(); // Берем последнее значение MACD, если данных мало
            }

            decimal histogram = macdValues.Last() - signal;

            return histogram;
        }


        /// <summary>
        /// Рассчитывает экспоненциальную скользящую среднюю (EMA).
        /// </summary>
        /// <param name="period">Период EMA.</param>
        /// <param name="data">Опциональный список данных (по умолчанию используется closePrices).</param>
        /// <returns>Значение EMA.</returns>
        private decimal CalculateEMA(int period, IEnumerable<decimal> data = null)
        {
            data ??= closePrices; // Используем закрытые цены, если не передан другой источник

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

        // Метод для вычисления простого скользящего среднего
        private decimal CalculateSMA( int period,IEnumerable<decimal> prices)
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчета SMA");

            return prices.TakeLast(period).Average();
        }

        private decimal CalculateHMA(int period, IEnumerable<decimal> prices)
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчета SMA");

            int halfPeriod = period / 2;
            int sqrtPeriod = (int)Math.Sqrt(period);

            decimal wmaHalf = CalculateWMA(halfPeriod, prices);
            decimal wmaFull = CalculateWMA(period, prices);

            List<decimal> diffWma = prices.Skip(prices.Count() - period).Select((p, i) =>
                (2 * wmaHalf - wmaFull)).ToList();

            return CalculateWMA(sqrtPeriod, diffWma);
        }

        private decimal CalculateWMA(int period, IEnumerable<decimal> prices )
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчета SMA");

            decimal denominator = (decimal)(period * (period + 1) / 2.0);
            decimal numerator = prices.Skip(prices.Count() - period)
                .Select((p, i) => p * (i + 1))
                .Sum();

            return (decimal)(numerator / denominator);
        }

        private Dictionary<string, decimal> CalculateMovingAverages(IEnumerable<decimal> prices)
        {
            int[] periods = { 10, 20, 30, 50, 100, 200 };
            var results = new Dictionary<string, decimal>();

            foreach (int period in periods)
            {
                if (prices.Count() >= period)
                {
                    results[$"SMA({period})"] = CalculateSMA(period, prices);
                    results[$"EMA({period})"] = CalculateEMA(period, prices);
                }
            }
            if (prices.Count() >= 9)
            {
                results["HMA(9)"] = CalculateHMA(9, prices);
            }

            return results;
        }
        //private decimal CalculateEMA(List<decimal> prices, int period)
        //{
        //    decimal multiplier = 2 / (period + 1);
        //    decimal ema = prices.Take(period).Average(); // Начальная EMA как SMA

        //    for (int i = period; i < prices.Count(); i++)
        //    {
        //        ema = (prices.ToList()[i] - ema) * multiplier + ema;
        //    }

        //    return ema;
        //}

        //private decimal CalculateMACD(int shortPeriod = 12, int longPeriod = 26, int signalPeriod = 9)
        //{
        //    //int period = 34;
        //    decimal emaShort = CalculateEMA(shortPeriod);
        //    decimal emaLong = CalculateEMA(longPeriod);
        //    decimal macd = emaShort - emaLong;

        //    // Рассчитать сигнальную линию как EMA от MACD
        //    List<decimal> macdLine = closePrices.Skip(longPeriod - 1).Select((_, i) => emaShort - emaLong).ToList();
        //    decimal signal = CalculateEMA(macdLine, signalPeriod);

        //    decimal histogram = macd - signal;
        //    return histogram;
        //}

        /// <summary>
        /// Рассчитывает индикатор MACD (Moving Average Convergence Divergence).
        /// </summary>
        /// <returns>
        /// Кортеж из трех значений: (MACD Line, Signal Line, Histogram).
        /// </returns>
        //private (decimal macdLine, decimal signalLine, decimal histogram) CalculateMACD()
        //{
        //    int fastPeriod = 12;  // Быстрая EMA
        //    int slowPeriod = 26;  // Медленная EMA
        //    int signalPeriod = 9; // EMA для сигнальной линии

        //    if (closePrices.Count() < slowPeriod)
        //        throw new InvalidOperationException("Недостаточно данных для расчёта MACD.");

        //    // Вычисляем быстрые и медленные EMA
        //    decimal fastEMA = CalculateEMA(fastPeriod);
        //    decimal slowEMA = CalculateEMA(slowPeriod);

        //    // MACD Line = Быстрая EMA - Медленная EMA
        //    decimal macdLine = fastEMA - slowEMA;

        //    // Signal Line = EMA от MACD Line
        //    decimal signalLine = CalculateSignalEMA(macdLine, signalPeriod);

        //    // Гистограмма = MACD Line - Signal Line
        //    decimal histogram = macdLine - signalLine;

        //    return (macdLine, signalLine, histogram);
        //}


        /// <summary>
        /// Рассчитывает сигнальную линию (Signal Line) как EMA от MACD Line.
        /// </summary>
        /// <param name="macdLine">Текущее значение MACD.</param>
        /// <param name="period">Период сигнальной линии.</param>
        /// <returns>Значение сигнальной линии.</returns>
        //private decimal CalculateSignalEMA(decimal macdLine, int period)
        //{
        //    if (signalLineHistory.Count == 0)
        //    {
        //        signalLineHistory.Add(macdLine);
        //        return macdLine;
        //    }

        //    decimal multiplier = 2m / (period + 1);
        //    decimal previousSignal = signalLineHistory.Last();
        //    decimal newSignal = (macdLine - previousSignal) * multiplier + previousSignal;

        //    signalLineHistory.Add(newSignal);
        //    return newSignal;
        //}

        /// <summary>
        /// Стохастический индекс относительной силы (Stochastic RSI)
        /// </summary>
        /// <returns>
        /// Индикатор, который сочетает в себе RSI и стохастический осциллятор.
        /// Значение выше 80 указывает на перекупленность, ниже 20 — на перепроданность.
        /// Рассчитывается на основе соотношения текущего RSI к его максимальному и минимальному значениям за заданный период.
        /// </returns>
        /// 

        private decimal CalculateStochasticRSI(int period = 14)
        {
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




        //private decimal CalculateStochasticRSI()
        //{
        //    int rsiPeriod = 14; // Период для расчёта RSI
        //    int stochPeriod = 14; // Период для стохастического осциллятора

        //    // Получаем список значений RSI за период
        //    var rsiValues = new List<decimal>();
        //    for (int i = rsiPeriod; i < closePrices.Count(); i++)
        //    {
        //        var periodRsi = CalculateRSI(); // Расчёт RSI для текущего периода
        //        rsiValues.Add(periodRsi);
        //    }

        //    // Получаем максимальное и минимальное значение RSI за заданный период stochPeriod
        //    var rsiMax = rsiValues.TakeLast(stochPeriod).Max();
        //    var rsiMin = rsiValues.TakeLast(stochPeriod).Min();

        //    // Рассчитываем текущий RSI
        //    var currentRsi = CalculateRSI(); // Текущий RSI

        //    // Стохастический RSI
        //    decimal stochRsi = (currentRsi - rsiMin) / (rsiMax - rsiMin) * 100;

        //    return stochRsi;
        //}


        /// <summary>
        /// Алгоритм Williams %R
        /// </summary>
        /// <returns>
        /// Индикатор перекупленности/перепроданности.
        /// Значение выше -20 указывает на перекупленность, ниже -80 — на перепроданность.
        /// </returns>
        private decimal CalculateWilliamsR()
        {
            int period = 14; // Период для расчёта Williams %R

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


        /// <summary>
        /// Рассчитывает BullBearPower как среднее между Bull Power и Bear Power.
        /// </summary>
        /// <returns>
        /// Значение выше 0 указывает на преобладание быков (покупателей),
        /// значение ниже 0 указывает на преобладание медведей (продавцов).
        /// </returns>
        private decimal CalculateBullBearPower()
        {
            int period = 13; // Используем стандартный период EMA

            // Рассчитываем EMA за заданный период
            decimal ema = CalculateEMA(period);

            // Bull Power = Максимальная цена - EMA
            decimal bullPower = highs.Last() - ema;

            // Bear Power = Минимальная цена - EMA
            decimal bearPower = lows.Last() - ema;

            // Итоговое значение как среднее между Bull и Bear Power
            return (bullPower + bearPower) / 2;
        }

        /// <summary>
        /// Рассчитывает экспоненциальную скользящую среднюю (EMA).
        /// </summary>
        /// <param name="period">Период EMA.</param>
        /// <returns>Значение EMA.</returns>
        //private decimal CalculateEMA(int period)
        //{
        //    if (closePrices.Count() < period)
        //        throw new InvalidOperationException("Недостаточно данных для расчёта EMA.");

        //    decimal multiplier = 2m / (period + 1);
        //    decimal ema = closePrices.Take(period).Average(); // Начальное значение — SMA

        //    foreach (var price in closePrices.Skip(period))
        //    {
        //        ema = (price - ema) * multiplier + ema;
        //    }

        //    return ema;
        //}


        /// <summary>
        /// Рассчитывает Ultimate Oscillator (UO).
        /// </summary>
        /// <returns>
        /// Значение осциллятора:
        /// - Выше 70 → перекупленность (возможная продажа).
        /// - Ниже 30 → перепроданность (возможная покупка).
        /// - Движение вверх из перепроданности → бычий сигнал.
        /// - Движение вниз из перекупленности → медвежий сигнал.
        /// </returns>
        private decimal CalculateUltimateOscillator()
        {
            int period1 = 7;
            int period2 = 14;
            int period3 = 28;

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
