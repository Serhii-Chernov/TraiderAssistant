using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services.TechnicalAnalysis
{
    internal class MovingAveragesService
    {

        internal List<TechnicalAnalysisNameValueActionStruct> PerformMovingAverages(IEnumerable<decimal> prices)
        {
            int[] periods = { 10, 20, 30, 50, 100, 200 };
            decimal currentPrice = prices.Last();
            List<TechnicalAnalysisNameValueActionStruct> MAsStructs = new List<TechnicalAnalysisNameValueActionStruct>();
            TechnicalAnalysisNameValueActionStruct MAStruct;
            foreach (int period in periods)
            {
                if (prices.Count() >= period)
                {

                    MAStruct = new TechnicalAnalysisNameValueActionStruct();
                    MAStruct.Name = $"SMA({period})";
                    MAStruct.Value = CalculateSMA(period, prices);
                    MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                    MAsStructs.Add(MAStruct);

                    MAStruct = new TechnicalAnalysisNameValueActionStruct();
                    MAStruct.Name = $"EMA({period})";
                    MAStruct.Value = CalculateEMA(period, prices);
                    MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                    MAsStructs.Add(MAStruct);
                }
            }
            if (prices.Count() >= 9)
            {
                MAStruct = new TechnicalAnalysisNameValueActionStruct();
                MAStruct.Name = $"HMA(9)";
                MAStruct.Value = CalculateHMA(9, prices);
                MAStruct.Action = GetMovingAverageAction(MAStruct.Value, currentPrice);
                MAsStructs.Add(MAStruct);
            }
            return MAsStructs;
        }

        internal decimal CalculateMovingAverageIndicator(List<TechnicalAnalysisNameValueActionStruct> MAsStructs)
        {
            if (MAsStructs == null || MAsStructs.Count == 0)
                throw new ArgumentException("List is null or empty.");

            var weights = GetMovingAverageWeights();

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
            if (prices.Count() < period)
                throw new InvalidOperationException("Not enough data to calculate HMA");

            int halfPeriod = period / 2;
            int sqrtPeriod = (int)Math.Sqrt(period);

            // Calculate WMA(halfPeriod) and WMA(period)
            var wmaHalf = CalculateWMA(halfPeriod, prices);
            var wmaFull = CalculateWMA(period, prices);

            // Create a list of differences: 2 * WMA(halfPeriod) - WMA(period)
            var weightedDiff = prices.TakeLast(period)
                .Select(p => 2 * wmaHalf - wmaFull)
                .ToList();

            // Calculate WMA(sqrtPeriod) based on the difference
            return CalculateWMA(sqrtPeriod, weightedDiff);
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
