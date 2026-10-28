using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class AdxOscillator : IOscillator
    {
        public string Name { get; set; } = "ADX";
        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            int period = 20;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            var highs = data.Select(k => k.HighPrice).ToList();
            var lows = data.Select(k => k.LowPrice).ToList();

            int n = highs.Count;
            if (n < period * 2)
                throw new ArgumentException("Not enough data to calculate ADX.");

            List<decimal> tr = new List<decimal>();
            List<decimal> plusDM = new List<decimal>();
            List<decimal> minusDM = new List<decimal>();

            for (int i = 1; i < n; i++)
            {
                decimal currentHigh = highs[i], previousHigh = highs[i - 1];
                decimal currentLow = lows[i], previousLow = lows[i - 1];
                decimal previousClose = closePrices[i - 1];

                tr.Add(Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - previousClose), Math.Abs(currentLow - previousClose))));

                decimal upMove = currentHigh - previousHigh;
                decimal downMove = previousLow - currentLow;
                plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
                minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);
            }

            decimal smoothTR = tr.Take(period).Sum();
            decimal smoothPlusDM = plusDM.Take(period).Sum();
            decimal smoothMinusDM = minusDM.Take(period).Sum();

            decimal finalPlusDI = smoothTR == 0 ? 0 : (smoothPlusDM / smoothTR) * 100;
            decimal finalMinusDI = smoothTR == 0 ? 0 : (smoothMinusDM / smoothTR) * 100;
            var dxValues = new List<decimal>
            {
                CalculateDx(finalPlusDI, finalMinusDI)
            };

            for (int i = period; i < tr.Count; i++)
            {
                smoothTR = smoothTR - (smoothTR / period) + tr[i];
                smoothPlusDM = smoothPlusDM - (smoothPlusDM / period) + plusDM[i];
                smoothMinusDM = smoothMinusDM - (smoothMinusDM / period) + minusDM[i];

                finalPlusDI = smoothTR == 0 ? 0 : (smoothPlusDM / smoothTR) * 100;
                finalMinusDI = smoothTR == 0 ? 0 : (smoothMinusDM / smoothTR) * 100;

                dxValues.Add(CalculateDx(finalPlusDI, finalMinusDI));
            }

            decimal adx = dxValues.Take(period).Average();
            foreach (decimal dx in dxValues.Skip(period))
                adx = ((adx * (period - 1)) + dx) / period;

            technicalAnalysisStruct.Name = Name;
            technicalAnalysisStruct.Value = adx;
            // Передаем разницу между силой покупателей и продавцов
            technicalAnalysisStruct.Action = GetAction(adx, finalPlusDI - finalMinusDI);

            return technicalAnalysisStruct;
        }

        private static decimal CalculateDx(decimal plusDi, decimal minusDi)
        {
            decimal diSum = plusDi + minusDi;
            return diSum == 0 ? 0 : Math.Abs(plusDi - minusDi) / diSum * 100;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            // value = ADX (сила тренда), extraValue = разница +DI и -DI (направление)
            if (value > 25 && extraValue > 0) return TradeAction.Buy;
            if (value > 25 && extraValue < 0) return TradeAction.Sell;
            return TradeAction.Neutral;
        }
    }
}
