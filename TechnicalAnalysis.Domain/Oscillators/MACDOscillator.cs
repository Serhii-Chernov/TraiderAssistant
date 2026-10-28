using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class MacdOscillator : IOscillator
    {
        public string Name { get; set; } = "MACD";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int shortPeriod = 12;
            int longPeriod = 26;
            int signalPeriod = 9;

            var closePrices = data.Select(k => k.ClosePrice).ToList();
            if (closePrices.Count < longPeriod + signalPeriod - 1)
                throw new InvalidOperationException("Not enough data to calculate MACD");

            var macdValues = CalculateMACD(closePrices, shortPeriod, longPeriod);
            var signalValues = CalculateEMA(macdValues, signalPeriod);

            decimal macd = macdValues[macdValues.Count - 1];
            decimal signal = signalValues[signalValues.Count - 1];
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

            // EMA outputs start at different source candles, so align them by candle index.
            int shortOffset = longPeriod - shortPeriod;
            return shortEma
                .Skip(shortOffset)
                .Zip(longEma, (fast, slow) => fast - slow)
                .ToList();
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
                decimal lastEma = emaValues[emaValues.Count - 1];
                decimal ema = (data[i] - lastEma) * multiplier + lastEma;
                emaValues.Add(ema);
            }

            return emaValues;
        }
    }


}
