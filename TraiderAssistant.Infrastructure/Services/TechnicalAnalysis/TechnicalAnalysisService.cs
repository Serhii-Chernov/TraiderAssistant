﻿using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TraiderAssistant.Infrastructure.Oscillators;

namespace TraiderAssistant.Infrastructure.Services.TechnicalAnalysis
{
    public class TechnicalAnalysisService
    {
        //private IEnumerable<decimal> closePrices;

        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            Console.WriteLine("PerformTechnicalAnalysis");

            IEnumerable<decimal> closePrices = data.Select(k => k.ClosePrice).ToList();

            var osciliators = PerformOscillators(data);
            var MAs = PerformMovingAverages(closePrices);

            var oscillatorIndicator = (double)CalculateOscillatorIndicatorValue(osciliators);
            var maIndicator = (double)CalculateMovingAverageIndicator(MAs);
            var generalIndicator = (oscillatorIndicator + maIndicator) / 2;

            TechnicalAnalysisResult technicalAnalysisResult = new TechnicalAnalysisResult(osciliators, MAs, oscillatorIndicator, maIndicator, generalIndicator);

            return technicalAnalysisResult;
        }


        private List<TechnicalAnalysisNameValueActionStruct> PerformOscillators(IEnumerable<BinanceSpotKline> data)
        {
            List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs = new List<TechnicalAnalysisNameValueActionStruct>();

            List<IOscillator> oscillatorsList = new List<IOscillator>
            {
                new RSIOscillator(),
                new StochasticKOscillator(),
                new CCIOscillator(),
                new ADXOscillator(),
                new AOOscillator(),
                new MomentumOscillator(),
                new MACDOscillator(),
                new UltimateOscillator(),
                new WilliamsROscillator(),
                new BullBearPowerOscillator(),
                new StochasticRSIOscillator()
            };

            //var osciliatorResults = new Dictionary<string, decimal>();

            TechnicalAnalysisNameValueActionStruct TechAnalysisStruct;
            foreach (var oscillator in oscillatorsList)
            {
                //osciliatorResults.Add($"{oscillator.Name}", oscillator.Calculate(data));
                try
                {

                    TechAnalysisStruct = new TechnicalAnalysisNameValueActionStruct(oscillator.Name, oscillator.Calculate(data));
                    osciliatorsStructs.Add(TechAnalysisStruct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

            }

            return osciliatorsStructs;
        }

        private decimal CalculateOscillatorIndicatorValue(List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs)
        {
            var osciliators = osciliatorsStructs.ToDictionary(o => o.Name, o => o.NormalizedValue);
            // Получаем значения индикаторов
            decimal normalizedRSI = osciliators["RSI"];
            decimal normalizedStochasticK = osciliators["StochasticK"];
            decimal normalizedCCI = osciliators["CCI"];
            decimal normalizedADX = osciliators["ADX"];
            decimal normalizedAO = osciliators["AO"];
            decimal normalizedMomentum = osciliators["Momentum"];
            decimal normalizedMACD = osciliators["MACD"];
            decimal normalizedUO = osciliators["UltimateOscillator"];
            decimal normalizedWilliamsR = osciliators["Williams%R"];
            decimal normalizedBullBearPower = osciliators["BullBearPower"];
            decimal normalizedStochasticRSI = osciliators["StochasticRSI"];

            // Весовые коэффициенты индикаторов
            decimal weightRSI = 0.15m;
            decimal weightStochasticK = 0.1m;
            decimal weightCCI = 0.1m;
            decimal weightADX = 0.15m;
            decimal weightAO = 0.1m;
            decimal weightMomentum = 0.1m;
            decimal weightMACD = 0.1m;
            decimal weightUO = 0.1m;
            decimal weightWilliamsR = 0.1m;
            decimal weightBullBearPower = 0.1m;
            decimal weightStochasticRSI = 0.1m;

            // Расчёт итогового значения индикатора (взвешенная сумма)
            decimal indicatorValue =
                normalizedRSI * weightRSI +
                normalizedStochasticK * weightStochasticK +
                normalizedCCI * weightCCI +
                normalizedADX * weightADX +
                normalizedAO * weightAO +
                normalizedMomentum * weightMomentum +
                normalizedMACD * weightMACD +
                normalizedUO * weightUO +
                normalizedWilliamsR * weightWilliamsR +
                normalizedBullBearPower * weightBullBearPower +
                normalizedStochasticRSI * weightStochasticRSI;

            return indicatorValue;
        }


        //private decimal CalculateMovingAverageIndicator(List<TechnicalAnalysisNameValueActionStruct> MAsStucts)
        //{
        //    if (MAsStucts.Count == 0)
        //        throw new ArgumentException("List is empty.");

        //    var movingAverages = MAsStucts.ToDictionary(ma => ma.Name, ma => ma.NormalizedValue);

        //    decimal min = movingAverages.Values.Min();
        //    decimal max = movingAverages.Values.Max();

        //    var weights = new Dictionary<string, decimal>
        //    {
        //        {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
        //        {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.05m}, {"EMA(50)", 0.1m},
        //        {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
        //        {"HMA(9)", 0.1m}
        //    };

        //    decimal indicatorValue = movingAverages
        //        .Where(kvp => weights.ContainsKey(kvp.Key))
        //        .Sum(kvp => kvp.Value * weights[kvp.Key]);

        //    return indicatorValue;
        //}

        private decimal CalculateMovingAverageIndicator(List<TechnicalAnalysisNameValueActionStruct> MAsStructs)
        {
            if (MAsStructs == null || MAsStructs.Count == 0)
                throw new ArgumentException("List is null or empty.");

            var weights = GetMovingAverageWeights();

            decimal weightedSum = 0;
            decimal totalWeight = 0;

            // Суммируем значения скользящих средних с их весами
            foreach (var ma in MAsStructs)
            {
                if (weights.TryGetValue(ma.Name, out decimal weight))
                {
                    weightedSum += ma.Value * weight;
                    totalWeight += weight;
                }
            }

            // Средневзвешенное значение
            decimal rawValue = totalWeight > 0 ? weightedSum / totalWeight : 0;

            // Вычисляем автоматические min и max на основе значений списка
            decimal min = MAsStructs.Min(ma => ma.Value);
            decimal max = MAsStructs.Max(ma => ma.Value);

            if (min == max)
                throw new InvalidOperationException("All values are the same; normalization is not possible.");

            // Нормализация в диапазон [0, 1]
            decimal normalizedValue = (rawValue - min) / (max - min);

            // Преобразование в диапазон [-100, 100]
            decimal finalValue = (normalizedValue * 200) - 100;

            return Math.Clamp(finalValue, -100, 100);
        }



        private Dictionary<string, decimal> GetMovingAverageWeights()
        {
            return new Dictionary<string, decimal>
            {
                {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
                {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.05m}, {"EMA(50)", 0.1m},
                {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
                {"HMA(9)", 0.1m}
            };
        }

        /// <summary>
        /// Рассчитывает экспоненциальную скользящую среднюю (EMA).
        /// </summary>
        /// <param name="period">Период EMA.</param>
        /// <param name="data">Опциональный список данных (по умолчанию используется closePrices).</param>
        /// <returns>Значение EMA.</returns>
        private decimal CalculateEMA(int period, IEnumerable<decimal> data)
        {
            //data ??= closePrices; // Используем закрытые цены, если не передан другой источник

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
        private decimal CalculateSMA(int period, IEnumerable<decimal> prices)
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
                2 * wmaHalf - wmaFull).ToList();

            return CalculateWMA(sqrtPeriod, diffWma);
        }

        private decimal CalculateWMA(int period, IEnumerable<decimal> prices)
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчета SMA");

            //decimal denominator = (decimal)(period * (period + 1) / 2.0);
            //decimal numerator = prices.Skip(prices.Count() - period)
            //    .Select((p, i) => p * (i + 1))
            //    .Sum();

            decimal denominator = period * (period + 1) / 2m;
            decimal numerator = prices.TakeLast(period)
                .Select((p, i) => p * (i + 1))
                .Sum();

            return numerator / denominator;
        }

        private List<TechnicalAnalysisNameValueActionStruct> PerformMovingAverages(IEnumerable<decimal> prices)
        {
            int[] periods = { 10, 20, 30, 50, 100, 200 };
            decimal currentPrice = prices.Last();
            List<TechnicalAnalysisNameValueActionStruct> MAsStructs = new List<TechnicalAnalysisNameValueActionStruct>();
            //var results = new Dictionary<string, decimal>();
            TechnicalAnalysisNameValueActionStruct MAStuct;
            foreach (int period in periods)
            {
                if (prices.Count() >= period)
                {

                    string name = $"SMA({period})";
                    decimal value = CalculateSMA(period, prices);
                    MAStuct = new TechnicalAnalysisNameValueActionStruct(name, value, currentPrice);
                    MAsStructs.Add(MAStuct);

                    MAStuct = new TechnicalAnalysisNameValueActionStruct(name, value, currentPrice);
                    MAStuct.Name = $"EMA({period})";
                    MAStuct.Value = CalculateEMA(period, prices);
                    MAsStructs.Add(MAStuct);
                    //results[$"SMA({period})"] = CalculateSMA(period, prices);
                    //results[$"EMA({period})"] = CalculateEMA(period, prices);
                }
            }
            if (prices.Count() >= 9)
            {
                //results["HMA(9)"] = CalculateHMA(9, prices);
                string name = $"HMA(9)";
                decimal value = CalculateHMA(9, prices);
                MAStuct = new TechnicalAnalysisNameValueActionStruct(name, value, currentPrice);
                MAsStructs.Add(MAStuct);
            }
            return MAsStructs;
        }
    }
    public class TechnicalAnalysisNameValueActionStruct
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
        private decimal normalizedValue;
        public decimal NormalizedValue
        {
            get
            {
                return normalizedValue;
            }
            private set
            {
                normalizedValue = value;
                Action = GetOscillatorAction(value);
            }
        }
        public string Action { get; set; }

        //List<string> oscillators = new List<string> { "RSI", "StochasticK", "CCI", "ADX", "AO", "Momentum", "MACD", "UltimateOscillator", "WilliamsR", "BullBearPower", "StochasticRSI" };

        public TechnicalAnalysisNameValueActionStruct(string MAName, decimal value, decimal currentPrice)
        {
            Name = MAName;
            Value = value;
            Action = GetMovingAverageAction(value, currentPrice);
        }

        public TechnicalAnalysisNameValueActionStruct(string oscillatorName, decimal value)
        {
            Name = oscillatorName;
            Value = value;
            NormalizedValue = NormalizeValue(Value, Name);

        }

        private decimal NormalizeValue(decimal value, string oscillatorName)
        {
            // Функция ограничения значений
            decimal Clamp(decimal val, decimal min, decimal max) => Math.Max(min, Math.Min(max, val));

            // Нормализация в зависимости от типа осциллятора
            return oscillatorName switch
            {
                "RSI" => value * 2 - 100,                    // Приводим к диапазону [-100, 100]
                "StochasticK" => value * 2 - 100,            // Аналогично RSI
                "CCI" => Clamp(value, -200, 200),            // Ограничиваем CCI
                "ADX" => value * 2 - 100,                    // Аналогично RSI
                "AO" => Clamp(value, -100, 100),             // AO уже в [-100, 100]
                "Momentum" => Clamp(value, -100, 100),       // Momentum уже в [-100, 100]
                "MACD" => value,                             // MACD оставляем как есть
                "UltimateOscillator" => value * 2 - 100,     // Ultimate Oscillator к [-100, 100]
                "Williams%R" => -Clamp(value, -100, 0),       // Williams %R инвертируем: [-100, 0] → [0, 100]
                "BullBearPower" => Clamp(value, -100, 100),  // Ограничиваем диапазон
                "StochasticRSI" => value * 2 - 100,          // Аналогично StochasticK
                //_ => Clamp(value, -100, 100)
                _ => throw new ArgumentException("Unknown oscillator name", nameof(oscillatorName))
            };
        }

        public string GetMovingAverageAction(decimal movingAverage, decimal currentPrice)
        {
            const decimal tolerance = 0.001m; // Допустимая погрешность для нейтрального сигнала

            if (currentPrice > movingAverage * (1 + tolerance))
                return TradeAction.TradeAction.Buy;
            else if (currentPrice < movingAverage * (1 - tolerance))
                return TradeAction.TradeAction.Sell;
            else
                return TradeAction.TradeAction.Neutral;
        }

        private string GetOscillatorAction(decimal indicatorValue)
        {
            if (indicatorValue < -15)
                return TradeAction.TradeAction.Sell;
            else if (indicatorValue > -15 && indicatorValue < 15)
                return TradeAction.TradeAction.Neutral;
            else
                return TradeAction.TradeAction.Buy;
        }
    }
}
