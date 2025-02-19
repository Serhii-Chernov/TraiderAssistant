using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{

    public class AOOscillator : IOscillator
    {
        public string Name { get; set; } = "AO";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            if (data == null || data.Count() < 34) // Проверка на достаточность данных
                throw new ArgumentException("Недостаточно данных для расчета AO (нужно >= 34)");

            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();

            decimal last = CalculateLast(data);
            decimal? previous = data.Count() > 34 ? CalculateLast(data.SkipLast(1)) : null;

            technicalAnalysisStruct.Name = Name;
            technicalAnalysisStruct.Value = last;
            technicalAnalysisStruct.Action = GetAction(last, previous);

            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? prevValue)
        {
            if (!prevValue.HasValue)
                return TradeAction.Neutral;

            if (value > 0 && prevValue.Value <= 0) return TradeAction.Buy;
            if (value < 0 && prevValue.Value >= 0) return TradeAction.Sell;

            return TradeAction.Neutral;
        }

        private decimal CalculateLast(IEnumerable<BinanceSpotKline> data)
        {
            var closePrices = data.Select(k => k.ClosePrice).ToList();

            if (closePrices.Count < 34) // Еще одна проверка
                throw new InvalidOperationException("Недостаточно данных для расчета AO");

            decimal sma5 = closePrices.Skip(closePrices.Count - 5).Average();
            decimal sma34 = closePrices.Skip(closePrices.Count - 34).Average();

            return sma5 - sma34;
        }
    }

}
