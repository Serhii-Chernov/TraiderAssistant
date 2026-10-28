using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Oscillators
{
    public class StochasticKOscillator : IOscillator
    {
        public string Name { get; set; } = "StochasticK";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new();
            technicalAnalysisStruct.Name = Name;
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();

            if (closePrices.Count < period)
                throw new ArgumentException("Not enough data to calculate StochasticK.");

            int startIndex = highs.Count - period;
            var recentHighs = highs.GetRange(startIndex, period);
            var recentLows = lows.GetRange(startIndex, period);

            decimal highestHigh = recentHighs.Max();
            decimal lowestLow = recentLows.Min();
            decimal currentClose = closePrices[closePrices.Count - 1];

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
            if (value > 80)
                return TradeAction.Sell;
            if (value < 20)
                return TradeAction.Buy;
            return TradeAction.Neutral;
        }
    }

}
