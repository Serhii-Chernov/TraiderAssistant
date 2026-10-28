using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services.TechnicalAnalysis
{
    public class MovingAveragesService
    {
        public List<TechnicalAnalysisNameValueActionStruct> PerformMovingAverages(IEnumerable<decimal> prices)
        {
            var priceList = prices?.ToList() ?? throw new ArgumentNullException(nameof(prices));
            if (priceList.Count == 0)
                throw new ArgumentException("Price list is empty.", nameof(prices));

            int[] periods = { 10, 20, 30, 50, 100, 200 };
            decimal currentPrice = priceList[^1];
            List<TechnicalAnalysisNameValueActionStruct> MAsStructs = new List<TechnicalAnalysisNameValueActionStruct>();
            TechnicalAnalysisNameValueActionStruct MAStruct;
            foreach (int period in periods)
            {
                if (priceList.Count >= period)
                {

                    MAStruct = new TechnicalAnalysisNameValueActionStruct();
                    MAStruct.Name = $"SMA({period})";
                    MAStruct.Value = CalculateSMA(period, priceList);
                    MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                    MAsStructs.Add(MAStruct);

                    MAStruct = new TechnicalAnalysisNameValueActionStruct();
                    MAStruct.Name = $"EMA({period})";
                    MAStruct.Value = CalculateEMA(period, priceList);
                    MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                    MAsStructs.Add(MAStruct);
                }
            }
            const int hmaPeriod = 9;
            int hmaLookback = hmaPeriod + (int)Math.Sqrt(hmaPeriod) - 1;
            if (priceList.Count >= hmaLookback)
            {
                MAStruct = new TechnicalAnalysisNameValueActionStruct();
                MAStruct.Name = "HMA(9)";
                MAStruct.Value = CalculateHMA(hmaPeriod, priceList);
                MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                MAsStructs.Add(MAStruct);
            }
            return MAsStructs;
        }

        // Новий метод
        public decimal CalculateMovingAverageIndicator(
            List<TechnicalAnalysisNameValueActionStruct> MAsStructs,
            Dictionary<string, decimal> weights)
        {
            if (MAsStructs == null || MAsStructs.Count == 0)
                throw new ArgumentException("List is null or empty.");

            decimal weightedSum = 0;
            decimal totalWeight = 0;

            foreach (var ma in MAsStructs)
            {
                if (weights.TryGetValue(ma.Name, out decimal weight))
                {
                    int actionValue = ma.Action switch
                    {
                        TradeAction.Buy => 1,
                        TradeAction.Sell => -1,
                        _ => 0
                    };

                    weightedSum += actionValue * weight;
                    totalWeight += weight;
                }
            }

            return totalWeight > 0 ? (weightedSum / totalWeight) * 100 : 0;
        }

        // Старий метод (викликає GetMovingAverageWeights())
        public decimal CalculateMovingAverageIndicator(List<TechnicalAnalysisNameValueActionStruct> MAsStructs)
        {
            var defaultWeights = GetMovingAverageWeights();
            return CalculateMovingAverageIndicator(MAsStructs, defaultWeights);
        }

        private Dictionary<string, decimal> GetMovingAverageWeights()
        {
            return new Dictionary<string, decimal>
            {
                {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
                {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.05m}, {"EMA(50)", 0.1m},
                {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
                {"HMA(9)", 0.1m}
            };
        }

        /// <summary>
        /// Calculates an exponential moving average (EMA).
        /// </summary>
        /// <param name="period">EMA period.</param>
        /// <param name="data">Optional data list (default is closePrices).</param>
        /// <returns>EMA value.</returns>
        private decimal CalculateEMA(int period, IEnumerable<decimal> data)
        {
            if (data.Count() < period)
                throw new InvalidOperationException("Not enough data to calculate EMA.");

            decimal multiplier = 2m / (period + 1);
            decimal ema = data.Take(period).Average(); // Initial value - SMA

            foreach (var price in data.Skip(period))
            {
                ema = (price - ema) * multiplier + ema;
            }

            return ema;
        }

        // Method for calculating simple moving average
        private decimal CalculateSMA(int period, IEnumerable<decimal> prices)
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Not enough data to calculate SMA");

            return prices.TakeLast(period).Average();
        }

        private decimal CalculateHMA(int period, IEnumerable<decimal> prices)
        {
            var priceList = prices.ToList();
            int sqrtPeriod = (int)Math.Sqrt(period);
            int requiredValues = period + sqrtPeriod - 1;

            if (priceList.Count < requiredValues)
                throw new InvalidOperationException("Not enough data to calculate HMA");

            int halfPeriod = period / 2;
            var weightedDifferences = new List<decimal>(sqrtPeriod);

            // HMA needs a rolling series of raw values before the final smoothing pass.
            for (int endExclusive = priceList.Count - sqrtPeriod + 1;
                 endExclusive <= priceList.Count;
                 endExclusive++)
            {
                var prefix = priceList.Take(endExclusive);
                decimal wmaHalf = CalculateWMA(halfPeriod, prefix);
                decimal wmaFull = CalculateWMA(period, prefix);
                weightedDifferences.Add(2 * wmaHalf - wmaFull);
            }

            return CalculateWMA(sqrtPeriod, weightedDifferences);
        }


        private decimal CalculateWMA(int period, IEnumerable<decimal> prices)
        {
            if (prices.Count() < period)
                throw new InvalidOperationException("Not enough data to calculate SMA");

            decimal denominator = period * (period + 1) / 2m;
            decimal numerator = prices.TakeLast(period)
                .Select((p, i) => p * (i + 1))
                .Sum();

            return numerator / denominator;
        }

        
        private string GetMovingAverageAction(decimal movingAverageValue, decimal currentPrice)
        {
            const decimal relativeTolerance = 0.001m; // 0.1%
            const decimal absoluteTolerance = 0.0001m; // Minimum threshold
            decimal tolerance = Math.Max(relativeTolerance * movingAverageValue, absoluteTolerance);

            if (currentPrice > movingAverageValue + tolerance)
                return TradeAction.Buy;
            if (currentPrice < movingAverageValue - tolerance)
                return TradeAction.Sell;
            return TradeAction.Neutral;

        }
    }
}
