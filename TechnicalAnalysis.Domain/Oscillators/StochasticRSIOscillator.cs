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
    internal class StochasticRSIOscillator : IOscillator
    {
        public string Name { get; set; } = "StochasticRSI";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();

            if (closePrices.Count < period)
                throw new InvalidOperationException("Not enough data to calculate Stochastic RSI");

            // Calculating RSI
            var rsiValues = closePrices.Select((_, i) =>
                i >= period ? CalculateRSI(closePrices.Skip(i - period).Take(period).ToList(), period) : 0).Skip(period).ToList();

            decimal highestRSI = rsiValues.TakeLast(period).Max();
            decimal lowestRSI = rsiValues.TakeLast(period).Min();
            decimal currentRSI = rsiValues.Last();

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
            return value > 80 ? TradeAction.Sell : (value < 20 ? TradeAction.Buy : TradeAction.Neutral);
        }

        private decimal CalculateRSI(List<decimal> prices, int period = 14)
        {
            decimal sumGain = 0;
            decimal sumLoss = 0;

            for (int i = 1; i < prices.Count; i++)
            {
                decimal delta = prices[i] - prices[i - 1];
                if (delta > 0)
                    sumGain += delta;
                else
                    sumLoss -= delta;
            }

            decimal avgGain = sumGain / period;
            decimal avgLoss = sumLoss / period;

            if (avgLoss == 0)
                return 100;
            if (avgGain == 0)
                return 0;

            decimal rs = avgGain / avgLoss;
            return 100 - 100 / (1 + rs);
        }
    }

}
