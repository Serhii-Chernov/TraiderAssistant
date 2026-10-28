using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    internal class WilliamsROscillator : IOscillator
    {
        public string Name { get; set; } = "Williams%R";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();

            if (closePrices.Count < period)
                throw new ArgumentException("Not enough data to calculate Williams%R.");

            int startIndex = highs.Count - period;
            var recentHighs = highs.GetRange(startIndex, period);
            var recentLows = lows.GetRange(startIndex, period);

            decimal highestHigh = recentHighs.Max();
            decimal lowestLow = recentLows.Min();
            decimal currentClose = closePrices[closePrices.Count - 1];

            decimal denominator = highestHigh - lowestLow;
            if (denominator == 0)
                technicalAnalysisStruct.Value=-50; // Average level (0 and -100 are extreme values)
            else
                technicalAnalysisStruct.Value = ((highestHigh - currentClose) / denominator) * -100;
            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);

            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            if (value > -20) return TradeAction.Sell;
            if (value < -80) return TradeAction.Buy;
            return TradeAction.Neutral;
        }
    }

}
