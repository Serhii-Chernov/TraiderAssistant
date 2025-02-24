using Binance.Net.Objects.Models.Spot;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    //
    internal class MACDOscillator : IOscillator
    {
        public string Name { get; set; } = "MACD";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int shortPeriod = 12;
            int longPeriod = 26;
            int signalPeriod = 9;

            var closePrices = data.Select(k => k.ClosePrice).ToList();
            if (closePrices.Count < longPeriod + signalPeriod)
                throw new InvalidOperationException("Not enough data to calculate MACD");

            var macdValues = CalculateMACD(closePrices, shortPeriod, longPeriod);
            var signalValues = CalculateEMA(macdValues, signalPeriod);

            decimal macd = macdValues.Last();
            decimal signal = signalValues.Last();
            decimal histogram = macd - signal; // The histogram is the MACD signal line

            return new TechnicalAnalysisNameValueActionStruct
            {
                Name = Name,
                Value = histogram,
                Action = GetAction(macd, signal)
            };
        }

        public string GetAction(decimal value,  decimal? extraValue = null)
        {
            if (value > extraValue) return TradeAction.Buy;
            if (value < extraValue) return TradeAction.Sell;
            return TradeAction.Neutral;
        }

        private List<decimal> CalculateMACD(List<decimal> prices, int shortPeriod, int longPeriod)
        {
            if (prices.Count < longPeriod)
                throw new InvalidOperationException("Not enough data to calculate MACD");

            var shortEma = CalculateEMA(prices, shortPeriod);
            var longEma = CalculateEMA(prices, longPeriod);

            return shortEma.Zip(longEma, (s, l) => s - l).ToList();
        }

        private List<decimal> CalculateEMA(List<decimal> data, int period)
        {
            if (data.Count < period)
                throw new InvalidOperationException($"Not enough data to calculate EMA({period})");

            List<decimal> emaValues = new List<decimal>();
            decimal multiplier = 2m / (period + 1);
            decimal sma = data.Take(period).Average();

            emaValues.Add(sma);
            for (int i = period; i < data.Count; i++)
            {
                decimal ema = (data[i] - emaValues.Last()) * multiplier + emaValues.Last();
                emaValues.Add(ema);
            }

            return emaValues;
        }
    }


}
