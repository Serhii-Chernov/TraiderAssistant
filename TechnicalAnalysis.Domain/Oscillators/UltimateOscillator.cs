using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class UltimateOscillator : IOscillator
    {
        public string Name { get; set; } = "UltimateOscillator";
        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            int period1 = 7;
            int period2 = 14;
            int period3 = 28;

            var closePrices = data.Select(x => x.ClosePrice).ToList();
            var highs = data.Select(x => x.HighPrice).ToList();
            var lows = data.Select(x => x.LowPrice).ToList();

            // BP/TR starts from the second candle, so 28 values need 29 candles.
            if (closePrices.Count < period3 + 1)
                throw new InvalidOperationException("Not enough data to calculate Ultimate Oscillator.");

            // Calculating BP and TR for each day
            List<decimal> bpList = new List<decimal>();
            List<decimal> trList = new List<decimal>();

            for (int i = 1; i < closePrices.Count; i++)
            {

                decimal close = closePrices[i];
                decimal prevClose = closePrices[i - 1];
                decimal low = lows[i];
                decimal high = highs[i];

                decimal bp = close - Math.Min(low, prevClose);
                decimal tr = Math.Max(high, prevClose) - Math.Min(low, prevClose);

                bpList.Add(bp);
                trList.Add(tr);
            }

            // Calculating the average BP and TR for three periods
            int start1 = bpList.Count - period1;
            int start2 = bpList.Count - period2;
            int start3 = bpList.Count - period3;

            decimal avgBP1 = bpList.GetRange(start1, period1).Sum();
            decimal avgTR1 = trList.GetRange(start1, period1).Sum();

            decimal avgBP2 = bpList.GetRange(start2, period2).Sum();
            decimal avgTR2 = trList.GetRange(start2, period2).Sum();

            decimal avgBP3 = bpList.GetRange(start3, period3).Sum();
            decimal avgTR3 = trList.GetRange(start3, period3).Sum();

            decimal term1 = avgTR1 == 0 ? 0 : avgBP1 / avgTR1;
            decimal term2 = avgTR2 == 0 ? 0 : avgBP2 / avgTR2;
            decimal term3 = avgTR3 == 0 ? 0 : avgBP3 / avgTR3;

            // Calculating Ultimate Oscillator
            technicalAnalysisStruct.Value = 100 * ((4 * term1 + 2 * term2 + 1 * term3) / 7m);

            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);

            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            if (value > 70)
                return TradeAction.Sell;
            if (value < 30)
                return TradeAction.Buy;
            return TradeAction.Neutral;
        }
    }
}
