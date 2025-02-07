using Binance.Net.Objects.Models.Spot;
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
                    TechAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
                    TechAnalysisStruct.Name = oscillator.Name;
                    TechAnalysisStruct.Value = oscillator.Calculate(data);
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
            decimal normalizedUO = osciliators["Ultimate Oscillator"];
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


        private decimal CalculateMovingAverageIndicator(List<TechnicalAnalysisNameValueActionStruct> MAsStucts)
        {
            if (MAsStucts.Count == 0)
                throw new ArgumentException("Словник скользящих сред пуст.");

            var movingAverages = MAsStucts.ToDictionary(ma => ma.Name, ma => ma.NormalizedValue);

            decimal min = movingAverages.Values.Min();
            decimal max = movingAverages.Values.Max();

            var weights = new Dictionary<string, decimal>
            {
                {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
                {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.1m}, {"EMA(50)", 0.1m},
                {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
                {"HMA(9)", 0.1m}
            };

            decimal indicatorValue = movingAverages
                .Where(kvp => weights.ContainsKey(kvp.Key))
                .Sum(kvp => kvp.Value * weights[kvp.Key]);

            return indicatorValue;
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

            decimal denominator = (decimal)(period * (period + 1) / 2.0);
            decimal numerator = prices.Skip(prices.Count() - period)
                .Select((p, i) => p * (i + 1))
                .Sum();

            return numerator / denominator;
        }

        private List<TechnicalAnalysisNameValueActionStruct> PerformMovingAverages(IEnumerable<decimal> prices)
        {
            int[] periods = { 10, 20, 30, 50, 100, 200 };
            List<TechnicalAnalysisNameValueActionStruct> MAsStructs = new List<TechnicalAnalysisNameValueActionStruct>();
            //var results = new Dictionary<string, decimal>();
            TechnicalAnalysisNameValueActionStruct MAStuct;
            foreach (int period in periods)
            {
                if (prices.Count() >= period)
                {
                    MAStuct = new TechnicalAnalysisNameValueActionStruct();
                    MAStuct.Name = $"SMA({period})";
                    MAStuct.Value = CalculateSMA(period, prices);
                    MAsStructs.Add(MAStuct);

                    MAStuct = new TechnicalAnalysisNameValueActionStruct();
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
                MAStuct = new TechnicalAnalysisNameValueActionStruct();
                MAStuct.Name = $"HMA(9)";
                MAStuct.Value = CalculateHMA(9, prices);
            }

            return MAsStructs;
        }
    }
    public class TechnicalAnalysisNameValueActionStruct
    {
        //private string name;
        private decimal value;
        private decimal normalizedValue;
        //private string action;
        public string Name { get; set; }
        public decimal Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
                NormalizedValue = NormalizeValue(Value, Name);
            }
        }
        public decimal NormalizedValue
        {
            get
            {
                return normalizedValue;
            }
            private set
            {
                normalizedValue = value;
                Action = PerformAction(value);
            }
        }
        public string Action { get; set; }

        //List<string> oscillators = new List<string> { "RSI", "StochasticK", "CCI", "ADX", "AO", "Momentum", "MACD", "UltimateOscillator", "WilliamsR", "BullBearPower", "StochasticRSI" };
        private decimal NormalizeValue(decimal value, string Name)
        {
            // Функция ограничения значений
            decimal Clamp(decimal val, decimal min, decimal max) => Math.Max(min, Math.Min(max, val));

            // Нормализация в зависимости от типа осциллятора
            return Name switch
            {
                "RSI" => value * 2 - 100,                    // Приводим к диапазону [-100, 100]
                "StochasticK" => value * 2 - 100,            // Аналогично RSI
                "CCI" => Clamp(value, -200, 200),            // Ограничиваем CCI
                "ADX" => value * 2 - 100,                    // Аналогично RSI
                "AO" => Clamp(value, -100, 100),             // AO уже в [-100, 100]
                "Momentum" => Clamp(value, -100, 100),       // Momentum уже в [-100, 100]
                "MACD" => value,                             // MACD оставляем как есть
                "UltimateOscillator" => value * 2 - 100,     // Ultimate Oscillator к [-100, 100]
                "WilliamsR" => -Clamp(value, -100, 0),       // Williams %R инвертируем: [-100, 0] → [0, 100]
                "BullBearPower" => Clamp(value, -100, 100),  // Ограничиваем диапазон
                "StochasticRSI" => value * 2 - 100,          // Аналогично StochasticK
                _ => Clamp(value, -100, 100)
                //_ => throw new ArgumentException("Unknown oscillator name", nameof(oscillatorName))
            };
        }
        private string PerformAction(decimal indicatorValue)
        {
            if (indicatorValue < -50)
                return TradeAction.TradeAction.ActiveSell;
            else if (indicatorValue < -15)
                return TradeAction.TradeAction.Sell;
            else if (indicatorValue > -15 && indicatorValue < 15)
                return TradeAction.TradeAction.Neutral;
            else if (indicatorValue > 50)
                return TradeAction.TradeAction.ActiveBuy;
            else
                return TradeAction.TradeAction.Buy;
        }
    }
}
