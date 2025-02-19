using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class ADXOscillator : IOscillator
    {
        public string Name{get; set;} = "ADX";
        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            int period = 20;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();
            
            int n = highs.Count();
            List<decimal> tr = new List<decimal>();
            List<decimal> plusDM = new List<decimal>();
            List<decimal> minusDM = new List<decimal>();

            for (int i = 1; i < n; i++)
            {
                decimal currentHigh = highs.ToList()[i], previousHigh = highs.ToList()[i - 1];
                decimal currentLow = lows.ToList()[i], previousLow = lows.ToList()[i - 1];
                decimal currentClose = closePrices.ToList()[i], previousClose = closePrices.ToList()[i - 1];

                // True Range (TR)
                tr.Add(Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - previousClose), Math.Abs(currentLow - previousClose))));

                // +DM and -DM
                decimal upMove = currentHigh - previousHigh;
                decimal downMove = previousLow - currentLow;
                plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
                minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);
            }

            // Smooth TR, +DM, and -DM
            decimal smoothTR = tr.Take(period).Sum();
            decimal smoothPlusDM = plusDM.Take(period).Sum();
            decimal smoothMinusDM = minusDM.Take(period).Sum();

            decimal adx = 0;
            for (int i = period; i < tr.Count; i++)
            {
                smoothTR = smoothTR - (smoothTR / period) + tr[i];
                smoothPlusDM = smoothPlusDM - (smoothPlusDM / period) + plusDM[i];
                smoothMinusDM = smoothMinusDM - (smoothMinusDM / period) + minusDM[i];

                decimal plusDI = (smoothPlusDM / smoothTR) * 100;
                decimal minusDI = (smoothMinusDM / smoothTR) * 100;
                decimal dx = (Math.Abs(plusDI - minusDI) / (plusDI + minusDI)) * 100;

                if (i == period)
                {
                    adx = dx;
                }
                else
                {
                    adx = ((adx * (period - 1)) + dx) / period;
                }
            }
            technicalAnalysisStruct.Name = Name;
            technicalAnalysisStruct.Value = adx;
            technicalAnalysisStruct.Action = GetAction(adx);

            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            return value > 25 ? TradeAction.Buy : TradeAction.Neutral;
        }
    }
}
