using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Services
{
    public class TechnicalAnalysisResult
    {
        public double OscillatorIndicator { get; set; }
        public string OscillatorResultText { get; set; }

        public double MAIndicator { get; set; }
        public string MAResultText { get; set; }

        public double GeneralIndicator { get; set; }
        public string GeneralResultText { get; set; }



        //private Dictionary<string, decimal> osciliators;
        //private Dictionary<string, decimal> movingAverages;
        public TechnicalAnalysisResult(Dictionary<string, decimal> osciliators, Dictionary<string, decimal> movingAverages)
        {
            //this.osciliators = osciliators;
            //this.movingAverages = movingAverages;
            OscillatorIndicator = (double)CalculateOscillatorIndicatorValue(osciliators);
            MAIndicator = (double)CalculateMovingAverageIndicator(movingAverages);
            GeneralIndicator = (OscillatorIndicator + MAIndicator) / 2;
            OscillatorResultText = UpdateResultText(OscillatorIndicator);
            MAResultText = UpdateResultText(MAIndicator);
            GeneralResultText = UpdateResultText(GeneralIndicator);
        }
        private string UpdateResultText(double indicatorValue)
        {
            if (indicatorValue < -50)
                return "Active sell";
            else if (indicatorValue < -15)
                return "Sell";
            else if (indicatorValue >-15 && indicatorValue<15)
                return "Neutral";
            else if (indicatorValue > 50)
                return "Active buy";
            else
                return "Buy";
        }

        

        private decimal CalculateMovingAverageIndicator(Dictionary<string, decimal> movingAverages)
        {
            if (movingAverages.Count == 0)
                throw new ArgumentException("Словник скользящих сред пуст.");

            decimal min = movingAverages.Values.Min();
            decimal max = movingAverages.Values.Max();

            decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));
            decimal Normalize(decimal value) => Clamp((value - min) / (max - min) * 200 - 100, -100, 100);

            var weights = new Dictionary<string, decimal>
            {
                {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
                {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.1m}, {"EMA(50)", 0.1m},
                {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
                {"HMA(9)", 0.1m}
            };

            decimal indicatorValue = movingAverages
                .Where(kvp => weights.ContainsKey(kvp.Key))
                .Sum(kvp => Normalize(kvp.Value) * weights[kvp.Key]);

            return indicatorValue;
        }



        private decimal CalculateOscillatorIndicatorValue(Dictionary<string, decimal> osciliators)
        {
            // Получаем значения индикаторов
            decimal rsi = osciliators["RSI"];
            decimal stochasticK = osciliators["StochasticK"];
            decimal cci = osciliators["CCI"];
            decimal adx = osciliators["ADX"];
            decimal ao = osciliators["AO"];
            decimal momentum = osciliators["Momentum"];
            decimal macd = osciliators["MACD"];
            decimal uo = osciliators["UltimateOscillator"];
            decimal williamsR = osciliators["WilliamsR"];
            decimal bullBearPower = osciliators["BullBearPower"];
            decimal stochasticRSI = osciliators["StochasticRSI"];

            // Функция ограничения значений
            decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

            // Нормализация индикаторов
            decimal normalizedRSI = rsi * 2 - 100;                    // Приводим к диапазону [-100, 100]
            decimal normalizedStochasticK = stochasticK * 2 - 100;    // Аналогично RSI
            decimal normalizedCCI = Clamp(cci, -200, 200);            // Ограничиваем CCI
            decimal normalizedADX = adx * 2 - 100;                    // Аналогично RSI
            decimal normalizedAO = Clamp(ao, -100, 100);              // AO уже в [-100, 100]
            decimal normalizedMomentum = Clamp(momentum, -100, 100);  // Momentum уже в [-100, 100]
            decimal normalizedMACD = macd;                            // MACD оставляем как есть
            decimal normalizedUO = uo * 2 - 100;                      // Ultimate Oscillator к [-100, 100]
            decimal normalizedWilliamsR = -Clamp(williamsR, -100, 0); // Williams %R инвертируем: [-100, 0] → [0, 100]
            decimal normalizedBullBearPower = Clamp(bullBearPower, -100, 100); // Ограничиваем диапазон
            decimal normalizedStochasticRSI = stochasticRSI * 2 - 100; // Аналогично StochasticK

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
    }

}
