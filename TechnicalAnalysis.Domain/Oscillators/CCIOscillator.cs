using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class CCIOscillator : IOscillator
    {
        public string Name { get; set; } = "CCI";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            int period = 20;
            var sortedData = data.OrderBy(k => k.OpenTime).ToList(); // Сортируем по времени
            if (sortedData.Count < period)
                throw new ArgumentException("Not enough data to calculate CCI");

            var typicalPrices = sortedData.Select(k => (k.HighPrice + k.LowPrice + k.ClosePrice) / 3).ToList();
            var recentTypicalPrices = typicalPrices.Skip(typicalPrices.Count - period).Take(period).ToList();
            decimal sma = recentTypicalPrices.Average();

            decimal meanDeviation = recentTypicalPrices
                                    .Select(tp => Math.Abs(tp - sma))
                                    .Average();

            technicalAnalysisStruct.Name = Name;

            if (meanDeviation == 0)
                technicalAnalysisStruct.Value = 0;
            else
                technicalAnalysisStruct.Value = (typicalPrices.Last() - sma) / (0.015m * meanDeviation);

            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);
            return technicalAnalysisStruct;
            
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            return value > 100 ? TradeAction.Sell : (value < -100 ? TradeAction.Buy : TradeAction.Neutral);
        }
    }

}
