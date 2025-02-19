using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services.TechnicalAnalysis
{
    internal class OscillatorsService
    {
       
        internal List<TechnicalAnalysisNameValueActionStruct> PerformOscillators(IEnumerable<BinanceSpotKline> data)
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

            TechnicalAnalysisNameValueActionStruct TechAnalysisStruct;
            foreach (var oscillator in oscillatorsList)
            {
                try
                {
                    TechAnalysisStruct = oscillator.Calculate(data); ;
                    osciliatorsStructs.Add(TechAnalysisStruct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            return osciliatorsStructs;
        }

        internal decimal CalculateOscillatorIndicatorValue(List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs)
        {
            var osciliators = osciliatorsStructs.ToDictionary(o => o.Name, o => o.Action);

            // Весовые коэффициенты индикаторов
            var weights = new Dictionary<string, decimal>
            {
                { "RSI", 0.15m },
                { "StochasticK", 0.1m },
                { "CCI", 0.1m },
                { "ADX", 0.15m },
                { "AO", 0.1m },
                { "Momentum", 0.1m },
                { "MACD", 0.1m },
                { "UltimateOscillator", 0.1m },
                { "Williams%R", 0.1m },
                { "BullBearPower", 0.1m },
                { "StochasticRSI", 0.1m }
            };

            decimal totalWeight = 0;
            decimal weightedSum = 0;

            foreach (var osc in osciliators)
            {
                decimal weight = weights.ContainsKey(osc.Key) ? weights[osc.Key] : 0;

                int actionValue = osc.Value switch
                {
                    TradeAction.Buy => 1,
                    TradeAction.Sell => -1,
                    _ => 0
                };

                weightedSum += actionValue * weight;
                totalWeight += weight;
            }

            return totalWeight == 0 ? 0 : weightedSum / totalWeight * 100;
        }
        //private decimal NormalizeOscillatorValue(decimal value, string oscillatorName)
        //{
        //    decimal Clamp(decimal val, decimal min, decimal max) => Math.Max(min, Math.Min(max, val));

        //    return oscillatorName switch
        //    {
        //        "RSI" => value * 2 - 100,
        //        "StochasticK" => value * 2 - 100,
        //        "CCI" => Clamp(value, -200, 200) / 2,
        //        "ADX" => value * 2 - 100,
        //        "AO" => Clamp(value, -100, 100),
        //        "Momentum" => Clamp(value, -100, 100),
        //        "MACD" => value, // Можно дополнительно нормализовать через стандартное отклонение
        //        "UltimateOscillator" => value * 2 - 100,
        //        "Williams%R" => -value,
        //        "BullBearPower" => Clamp(value, -100, 100),
        //        "StochasticRSI" => value * 2 - 100,
        //        _ => throw new ArgumentException("Unknown oscillator name", nameof(oscillatorName))
        //    };
        //}

        private string GetOscillatorAction(decimal value, string oscillatorName, decimal? extraValue = null)
        {
            return oscillatorName switch
            {
                "RSI" => value > 70 ? TradeAction.Sell : (value < 30 ? TradeAction.Buy : TradeAction.Neutral),
                "StochasticK" => value > 80 ? TradeAction.Sell : (value < 20 ? TradeAction.Buy : TradeAction.Neutral),
                "CCI" => value > 100 ? TradeAction.Sell : (value < -100 ? TradeAction.Buy : TradeAction.Neutral),
                "ADX" => value > 25 ? TradeAction.Buy : TradeAction.Neutral, // ADX только показывает силу тренда

                // Доработанный AO
                "AO" => extraValue.HasValue
                    ? (value > 0 && extraValue.Value <= 0 ? TradeAction.Buy :
                       value < 0 && extraValue.Value >= 0 ? TradeAction.Sell :
                       TradeAction.Neutral)
                    : TradeAction.Neutral, // extraValue — предыдущее значение AO

                // Доработанный MACD (extraValue — сигнальная линия)
                "MACD" => extraValue.HasValue
                    ? (value > extraValue.Value ? TradeAction.Buy :
                       value < extraValue.Value ? TradeAction.Sell :
                       TradeAction.Neutral)
                    : TradeAction.Neutral,

                "UltimateOscillator" => value > 70 ? TradeAction.Sell : (value < 30 ? TradeAction.Buy : TradeAction.Neutral),
                "Williams%R" => value < -20 ? TradeAction.Sell : (value > -80 ? TradeAction.Buy : TradeAction.Neutral),
                "BullBearPower" => value > 0 ? TradeAction.Buy : TradeAction.Sell,
                "StochasticRSI" => value > 80 ? TradeAction.Sell : (value < 20 ? TradeAction.Buy : TradeAction.Neutral),
                "Momentum" => value > 0 ? TradeAction.Buy : TradeAction.Sell,
                _ => TradeAction.Neutral
            };
        }




    }
}
