using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain.Oscillators;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services.TechnicalAnalysis
{
    internal class OscillatorsService
    {
        private readonly List<IOscillator> _oscillatorsList = new List<IOscillator>
        {
            new RsiOscillator(),
            new StochasticKOscillator(),
            new CciOscillator(),
            new AdxOscillator(),
            new AOOscillator(),
            new MomentumOscillator(),
            new MacdOscillator(),
            new UltimateOscillator(),
            new WilliamsROscillator(),
            new BullBearPowerOscillator(),
            new StochasticRsiOscillator()
        };

        internal List<TechnicalAnalysisNameValueActionStruct> PerformOscillators(IEnumerable<BinanceSpotKline> data)
        {
            List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs = new List<TechnicalAnalysisNameValueActionStruct>();

            TechnicalAnalysisNameValueActionStruct TechAnalysisStruct;
            foreach (var oscillator in _oscillatorsList)
            {
                TechAnalysisStruct = oscillator.Calculate(data);
                osciliatorsStructs.Add(TechAnalysisStruct);
            }
            return osciliatorsStructs;
        }

        // Новий метод, який приймає ваги
        internal decimal CalculateOscillatorIndicatorValue(
            List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs,
            Dictionary<string, decimal> weights)
        {
            var osciliators = osciliatorsStructs.ToDictionary(o => o.Name, o => o.Action);

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

        // Старий метод (перевантаження), який використовується за замовчуванням
        internal decimal CalculateOscillatorIndicatorValue(List<TechnicalAnalysisNameValueActionStruct> osciliatorsStructs)
        {
            var defaultWeights = new Dictionary<string, decimal>
            {
                { "RSI", 0.15m }, { "StochasticK", 0.1m }, { "CCI", 0.1m },
                { "ADX", 0.15m }, { "AO", 0.1m }, { "Momentum", 0.1m },
                { "MACD", 0.1m }, { "UltimateOscillator", 0.1m },
                { "Williams%R", 0.1m }, { "BullBearPower", 0.1m }, { "StochasticRSI", 0.1m }
            };

            return CalculateOscillatorIndicatorValue(osciliatorsStructs, defaultWeights);
        }
    }
}
