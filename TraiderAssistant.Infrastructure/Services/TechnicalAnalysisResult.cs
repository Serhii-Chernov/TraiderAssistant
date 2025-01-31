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
        public double MAIndicator { get; set; }
        public string OscillatorResultText { get; set; }
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
            UpdateOscillatorResultText();
            UpdateMAResultText();
            UpdateGeneralResultText();
        }
        private void UpdateGeneralResultText()
        {
            // Обновляем текст и угол стрелки
            if (GeneralIndicator < -50)
                GeneralResultText = "Активно продавать";
            else if (GeneralIndicator < 0)
                GeneralResultText = "Продавать";
            else if (GeneralIndicator == 0)
                GeneralResultText = "Нейтрально";
            else if (GeneralIndicator > 50)
                GeneralResultText = "Активно покупать";
            else
                GeneralResultText = "Покупать";
        }

        private void UpdateOscillatorResultText()
        {
                // Обновляем текст и угол стрелки
                if (OscillatorIndicator < -50)
                    OscillatorResultText = "Активно продавать";
                else if (OscillatorIndicator < 0)
                    OscillatorResultText = "Продавать";
                else if (OscillatorIndicator == 0)
                    OscillatorResultText = "Нейтрально";
                else if (OscillatorIndicator > 50)
                    OscillatorResultText = "Активно покупать";
                else
                    OscillatorResultText = "Покупать";
        }

        private void UpdateMAResultText()
        {
            // Обновляем текст и угол стрелки
            if (MAIndicator < -50)
                MAResultText = "Активно продавать";
            else if (MAIndicator < 0)
                MAResultText = "Продавать";
            else if (MAIndicator == 0)
                MAResultText = "Нейтрально";
            else if (MAIndicator > 50)
                MAResultText = "Активно покупать";
            else
                MAResultText = "Покупать";
        }


        private decimal CalculateMovingAverageIndicator(Dictionary<string, decimal> movingAverages)
        {
            // Получаем значения скользящих средних
            decimal sma10 = movingAverages["SMA(10)"];
            decimal ema10 = movingAverages["EMA(10)"];
            decimal sma20 = movingAverages["SMA(20)"];
            decimal ema20 = movingAverages["EMA(20)"];
            decimal sma30 = movingAverages["SMA(30)"];
            decimal ema30 = movingAverages["EMA(30)"];
            decimal sma50 = movingAverages["SMA(50)"];
            decimal ema50 = movingAverages["EMA(50)"];
            decimal sma100 = movingAverages["SMA(100)"];
            decimal ema100 = movingAverages["EMA(100)"];
            decimal sma200 = movingAverages["SMA(200)"];
            decimal ema200 = movingAverages["EMA(200)"];
            decimal hma9 = movingAverages["HMA(9)"];

            // Функция ограничения значений
            decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

            // Нормализация значений (переводим в диапазон [-100, 100] относительно SMA200)
            decimal reference = sma200;
            decimal Normalize(decimal value) => Clamp((value - reference) / reference * 100, -100, 100);

            decimal normalizedSMA10 = Normalize(sma10);
            decimal normalizedEMA10 = Normalize(ema10);
            decimal normalizedSMA20 = Normalize(sma20);
            decimal normalizedEMA20 = Normalize(ema20);
            decimal normalizedSMA30 = Normalize(sma30);
            decimal normalizedEMA30 = Normalize(ema30);
            decimal normalizedSMA50 = Normalize(sma50);
            decimal normalizedEMA50 = Normalize(ema50);
            decimal normalizedSMA100 = Normalize(sma100);
            decimal normalizedEMA100 = Normalize(ema100);
            decimal normalizedSMA200 = Normalize(sma200);
            decimal normalizedEMA200 = Normalize(ema200);
            decimal normalizedHMA9 = Normalize(hma9);

            // Весовые коэффициенты (можно подстроить под стратегию)
            decimal weightSMA10 = 0.05m;
            decimal weightEMA10 = 0.1m;
            decimal weightSMA20 = 0.05m;
            decimal weightEMA20 = 0.1m;
            decimal weightSMA30 = 0.05m;
            decimal weightEMA30 = 0.1m;
            decimal weightSMA50 = 0.1m;
            decimal weightEMA50 = 0.1m;
            decimal weightSMA100 = 0.1m;
            decimal weightEMA100 = 0.1m;
            decimal weightSMA200 = 0.05m;
            decimal weightEMA200 = 0.05m;
            decimal weightHMA9 = 0.1m;

            // Итоговое значение индикатора (взвешенная сумма)
            decimal indicatorValue =
                normalizedSMA10 * weightSMA10 +
                normalizedEMA10 * weightEMA10 +
                normalizedSMA20 * weightSMA20 +
                normalizedEMA20 * weightEMA20 +
                normalizedSMA30 * weightSMA30 +
                normalizedEMA30 * weightEMA30 +
                normalizedSMA50 * weightSMA50 +
                normalizedEMA50 * weightEMA50 +
                normalizedSMA100 * weightSMA100 +
                normalizedEMA100 * weightEMA100 +
                normalizedSMA200 * weightSMA200 +
                normalizedEMA200 * weightEMA200 +
                normalizedHMA9 * weightHMA9;

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
