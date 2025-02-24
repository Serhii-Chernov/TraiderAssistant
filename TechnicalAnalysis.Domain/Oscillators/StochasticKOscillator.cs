using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    //
    public class StochasticKOscillator : IOscillator
    {
        public string Name { get; set; } = "StochasticK";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();

            if (closePrices.Count < period)
                throw new ArgumentException("Not enough data to calculate StochasticK.");

            decimal highestHigh = highs.TakeLast(period).Max();
            decimal lowestLow = lows.TakeLast(period).Min();
            decimal currentClose = closePrices.Last();

            decimal denominator = highestHigh - lowestLow;
            if (denominator == 0)
                technicalAnalysisStruct.Value = 50; // When High == Low, stochastic is neutral.
            else
                technicalAnalysisStruct.Value = ((currentClose - lowestLow) / denominator) * 100;

            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);
            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            return value > 80 ? TradeAction.Sell : (value < 20 ? TradeAction.Buy : TradeAction.Neutral);
        }
    }

}
