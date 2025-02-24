using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    //
    public class MomentumOscillator : IOscillator
    {
        public string Name { get; set; } = "Momentum";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            int period = 10;
            var closePrices = data.Select(k => k.ClosePrice).ToList();

            if (closePrices.Count <= period)
                throw new InvalidOperationException("Not enough data to calculate Momentum");

            decimal currentClose = closePrices.Last();
            decimal previousClose = closePrices[closePrices.Count - period - 1];
            technicalAnalysisStruct.Name = Name;
            technicalAnalysisStruct.Value = currentClose - previousClose;
            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);

            return technicalAnalysisStruct;  // The difference between the current and previous price
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            return value > 0 ? TradeAction.Buy : TradeAction.Sell;
        }
    }
}
