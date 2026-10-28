using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    internal class StochasticRsiOscillator : IOscillator
    {
        public string Name { get; set; } = "StochasticRSI";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();

            if (closePrices.Count < period * 2)
                throw new InvalidOperationException("Not enough data to calculate Stochastic RSI");

            var rsiValues = CalculateRsiSeries(closePrices, period);

            var recentRsis = rsiValues.TakeLast(period).ToList();
            decimal highestRSI = recentRsis.Max();
            decimal lowestRSI = recentRsis.Min();
            decimal currentRSI = recentRsis[^1];

            decimal denominator = highestRSI - lowestRSI;
            if (denominator == 0)
                technicalAnalysisStruct.Value = 50; // Intermediate level
            else
                technicalAnalysisStruct.Value = ((currentRSI - lowestRSI) / denominator) * 100;

            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);

            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            if (value > 80) return TradeAction.Sell;
            if (value < 20) return TradeAction.Buy;
            return TradeAction.Neutral;
        }

        private static List<decimal> CalculateRsiSeries(IReadOnlyList<decimal> prices, int period)
        {
            decimal sumGain = 0;
            decimal sumLoss = 0;

            for (int i = 1; i <= period; i++)
            {
                decimal delta = prices[i] - prices[i - 1];
                if (delta > 0)
                    sumGain += delta;
                else
                    sumLoss -= delta;
            }

            decimal avgGain = sumGain / period;
            decimal avgLoss = sumLoss / period;
            var result = new List<decimal>(prices.Count - period)
            {
                CalculateRsi(avgGain, avgLoss)
            };

            for (int i = period + 1; i < prices.Count; i++)
            {
                decimal delta = prices[i] - prices[i - 1];
                decimal gain = delta > 0 ? delta : 0;
                decimal loss = delta < 0 ? -delta : 0;

                avgGain = ((avgGain * (period - 1)) + gain) / period;
                avgLoss = ((avgLoss * (period - 1)) + loss) / period;
                result.Add(CalculateRsi(avgGain, avgLoss));
            }

            return result;
        }

        private static decimal CalculateRsi(decimal avgGain, decimal avgLoss)
        {
            if (avgGain == 0 && avgLoss == 0)
                return 50;

            if (avgLoss == 0)
                return 100;
            if (avgGain == 0)
                return 0;

            decimal rs = avgGain / avgLoss;
            return 100 - 100 / (1 + rs);
        }
    }

}
