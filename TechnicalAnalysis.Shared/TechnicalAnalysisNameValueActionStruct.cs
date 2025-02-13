using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TechnicalAnalysis.Shared
{
    public class TechnicalAnalysisNameValueActionStruct
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
        [JsonIgnore]
        public decimal NormalizedValue { get; set; }
        public string Action { get;  set; }  


        // Пустой конструктор для десериализации
        public TechnicalAnalysisNameValueActionStruct() { }

        // Конструктор для скользящих средних
        //public TechnicalAnalysisNameValueActionStruct(string maName, decimal value, decimal currentPrice)
        //{
        //    Name = maName;
        //    Value = value;
        //    Action = GetMovingAverageAction(value, currentPrice);
        //}

        //// Конструктор для осцилляторов
        //public TechnicalAnalysisNameValueActionStruct(string oscillatorName, decimal value)
        //{
        //    Name = oscillatorName;
        //    Value = value;
        //    Action = GetOscillatorAction(NormalizedValue);
        //}

        // Нормализация значений
        private decimal NormalizeValue(decimal value, string oscillatorName)
        {
            decimal Clamp(decimal val, decimal min, decimal max) => Math.Max(min, Math.Min(max, val));

            return oscillatorName switch
            {
                "RSI" => value * 2 - 100,
                "StochasticK" => value * 2 - 100,
                "CCI" => Clamp(value, -200, 200),
                "ADX" => value * 2 - 100,
                "AO" => Clamp(value, -100, 100),
                "Momentum" => Clamp(value, -100, 100),
                "MACD" => value,
                "UltimateOscillator" => value * 2 - 100,
                "Williams%R" => -Clamp(value, -100, 0),
                "BullBearPower" => Clamp(value, -100, 100),
                "StochasticRSI" => value * 2 - 100,
                _ => throw new ArgumentException("Unknown oscillator name", nameof(oscillatorName))
            };
        }

        // Метод для определения действия на основе скользящих средних
        private string GetMovingAverageAction(decimal movingAverage, decimal currentPrice)
        {
            const decimal tolerance = 0.001m;

            if (currentPrice > movingAverage * (1 + tolerance))
                return TradeAction.Buy;
            else if (currentPrice < movingAverage * (1 - tolerance))
                return TradeAction.Sell;
            else
                return TradeAction.Neutral;
        }

        // Метод для определения действия на основе осцилляторов
        private string GetOscillatorAction(decimal indicatorValue)
        {
            if (indicatorValue < -15)
                return TradeAction.Sell;
            else if (indicatorValue > 15)
                return TradeAction.Buy;
            else
                return TradeAction.Neutral;
        }
    }
}
