using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;
using TechnicalAnalysis.Domain.Models;

namespace TechnicalAnalysis.Domain
{
    public class BinanceService
    {
        private readonly BinanceRestClient _client;
        private readonly ILogger<BinanceService> _logger;

        internal const decimal MaximumMissingCandlePercent = 0.5m;
        internal static readonly TimeSpan MaximumIndividualGap = TimeSpan.FromHours(24);

        public BinanceService() : this(null)
        {
        }

        public BinanceService(ILogger<BinanceService> logger)
        {
            _logger = logger;
            _client = new BinanceRestClient();
        }

        public async Task<List<BinanceSpotKline>> GetFullHistoryDataAsync(CurrencyPair currencyPair,
            KlineInterval interval, DateTime startTime, DateTime endTime)
        {
            return (await GetFullHistoryDataBatchAsync(currencyPair, interval, startTime, endTime)).Candles;
        }

        internal async Task<HistoricalDataBatch> GetFullHistoryDataBatchAsync(CurrencyPair currencyPair,
            KlineInterval interval, DateTime startTime, DateTime endTime)
        {
            var allKlines = new ConcurrentBag<BinanceSpotKline>();
            var tasks = new List<Task>();

            // Ограничиваем до 10 одновременных запросов к Binance
            var semaphore = new SemaphoreSlim(10);

            DateTime currentStartTime = startTime;
            var intervalSpan = GetIntervalTimeSpan(interval);
            TimeSpan chunkDuration = TimeSpan.FromMinutes(intervalSpan.TotalMinutes * 1000);

            while (currentStartTime < endTime)
            {
                DateTime chunkEndTime = currentStartTime + chunkDuration;
                if (chunkEndTime > endTime) chunkEndTime = endTime;

                var start = currentStartTime; // Захват переменной для лямбды
                var end = chunkEndTime;

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(
                            currencyPair.ToString(), interval, start, end, limit: 1000);

                        if (!result.Success)
                            throw new InvalidOperationException(
                                $"Binance history request failed for {currencyPair} {interval} " +
                                $"[{start:O}, {end:O}): {result.Error?.Message ?? "unknown error"}");

                        foreach (var kline in result.Data)
                            allKlines.Add((BinanceSpotKline)kline);
                    }
                    finally
                    {
                        // Искусственная задержка, чтобы не ловить ошибку 429 Too Many Requests
                        await Task.Delay(100);
                        semaphore.Release();
                    }
                }));

                currentStartTime = chunkEndTime;
            }

            await Task.WhenAll(tasks);

            // End time is exclusive. A candle is usable only after it has fully closed.
            DateTime effectiveEnd = endTime < DateTime.UtcNow ? endTime : DateTime.UtcNow;
            var ordered = allKlines
                .Where(kline => kline.OpenTime < endTime && kline.CloseTime <= effectiveEnd)
                .GroupBy(kline => kline.OpenTime)
                .Select(group => group.First())
                .OrderBy(kline => kline.OpenTime)
                .ToList();

            if (ordered.Count == 0)
                throw new InvalidOperationException(
                    $"Binance returned no closed candles for {currencyPair} {interval} [{startTime:O}, {endTime:O}).");

            var quality = AnalyzeDataQuality(
                currencyPair.ToString(),
                interval.ToString(),
                ordered.Select(kline => kline.OpenTime).ToList(),
                intervalSpan);
            if (quality.MissingCandles > 0)
            {
                _logger?.LogWarning(
                    "Historical data contains acceptable gaps. Asset: {Asset}, Timeframe: {Timeframe}, MissingCandles: {MissingCandles}, MissingPercent: {MissingPercent}, MaximumGapCandles: {MaximumGapCandles}",
                    quality.Asset,
                    quality.Timeframe,
                    quality.MissingCandles,
                    quality.MissingPercent,
                    quality.MaximumGapCandles);
            }

            return new HistoricalDataBatch
            {
                Candles = ordered,
                Quality = quality
            };
        }

        internal static HistoricalDataQualityResult AnalyzeDataQuality(
            string asset,
            string timeframe,
            IReadOnlyList<DateTime> openTimes,
            TimeSpan interval)
        {
            if (openTimes == null || openTimes.Count == 0)
                throw new ArgumentException("At least one candle open time is required.", nameof(openTimes));
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval));

            var result = new HistoricalDataQualityResult
            {
                Asset = asset,
                Timeframe = timeframe,
                ActualCandles = openTimes.Count
            };

            for (int index = 1; index < openTimes.Count; index++)
            {
                TimeSpan distance = openTimes[index] - openTimes[index - 1];
                if (distance <= TimeSpan.Zero)
                    throw new InvalidOperationException(
                        $"Historical candles for {asset} {timeframe} are not strictly ordered at {openTimes[index]:O}.");
                if (distance.Ticks % interval.Ticks != 0)
                    throw new InvalidOperationException(
                        $"Historical candle for {asset} {timeframe} is not aligned to the {interval} interval: " +
                        $"{openTimes[index - 1]:O} -> {openTimes[index]:O}.");

                int missingCandles = checked((int)(distance.Ticks / interval.Ticks) - 1);
                if (missingCandles <= 0)
                    continue;

                TimeSpan missingDuration = TimeSpan.FromTicks(interval.Ticks * missingCandles);
                if (missingDuration > MaximumIndividualGap)
                {
                    throw new InvalidOperationException(
                        $"Historical data gap for {asset} {timeframe} exceeds {MaximumIndividualGap.TotalHours:0} hours: " +
                        $"expected {openTimes[index - 1] + interval:O}, received {openTimes[index]:O}, " +
                        $"missing {missingCandles} candles.");
                }

                result.MissingCandles += missingCandles;
                result.MaximumGapCandles = Math.Max(result.MaximumGapCandles, missingCandles);
                result.Gaps.Add(new HistoricalDataGap
                {
                    ExpectedOpenTime = openTimes[index - 1] + interval,
                    ReceivedOpenTime = openTimes[index],
                    MissingCandles = missingCandles
                });
            }

            result.ExpectedCandles = result.ActualCandles + result.MissingCandles;
            result.MissingPercent = result.ExpectedCandles == 0
                ? 0m
                : Math.Round((decimal)result.MissingCandles / result.ExpectedCandles * 100m, 4);
            if (result.MissingPercent > MaximumMissingCandlePercent)
            {
                throw new InvalidOperationException(
                    $"Historical data for {asset} {timeframe} is incomplete: " +
                    $"{result.MissingCandles} of {result.ExpectedCandles} candles are missing " +
                    $"({result.MissingPercent:0.####}%), limit is {MaximumMissingCandlePercent:0.####}%.");
            }

            return result;
        }

        public async Task<List<KlineData>> GetChartDataAsync(CurrencyPair currencyPair, DateTime startTime,
            DateTime endTime)
        {
            var interval = GetKlineInterval(startTime, endTime);

            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime,
                endTime, limit: 1500);
            if (result.Success)
            {
                List<KlineData> klineDataList = new List<KlineData>();
                KlineData klineData;
                foreach (var item in result.Data)
                {
                    klineData = new KlineData();
                    klineData.OpenTime = item.OpenTime;
                    klineData.CloseTime = item.CloseTime;

                    klineData.OpenPrice = item.OpenPrice;
                    klineData.ClosePrice = item.ClosePrice;

                    klineData.LowPrice = item.LowPrice;
                    klineData.HighPrice = item.HighPrice;
                    klineDataList.Add(klineData);
                }

                return klineDataList;
            }
            else
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Binance API error");
            }
        }

        private KlineInterval GetKlineInterval(DateTime startTime, DateTime endTime)
        {
            TimeSpan duration = endTime - startTime;
            int targetPoints = 400; // Target number of data points

            double secondsPerCandle = duration.TotalSeconds / targetPoints;

            // List of intervals sorted by duration
            var intervals = new List<(KlineInterval Interval, double Seconds)>
            {
                (KlineInterval.OneSecond, 1),
                (KlineInterval.OneMinute, 60),
                (KlineInterval.ThreeMinutes, 180),
                (KlineInterval.FiveMinutes, 300),
                (KlineInterval.FifteenMinutes, 900),
                (KlineInterval.ThirtyMinutes, 1800),
                (KlineInterval.OneHour, 3600),
                (KlineInterval.TwoHour, 7200),
                (KlineInterval.FourHour, 14400),
                (KlineInterval.SixHour, 21600),
                (KlineInterval.EightHour, 28800),
                (KlineInterval.TwelveHour, 43200),
                (KlineInterval.OneDay, 86400),
                (KlineInterval.ThreeDay, 259200),
                (KlineInterval.OneWeek, 604800),
                (KlineInterval.OneMonth, 2592000)
            };

            // Select the interval closest to `secondsPerCandle`
            var ordered = intervals.OrderBy(i => Math.Abs(i.Seconds - secondsPerCandle)).ToList();
            return ordered[0].Interval;
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForIndicatorsAsync(CurrencyPair currencyPair,
            DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
        {
            // Taking into account the coefficient for the current timeframe
            int intervalMinutes = interval switch
            {
                KlineInterval.OneMinute => 1,
                KlineInterval.FiveMinutes => 5,
                KlineInterval.FifteenMinutes => 15,
                KlineInterval.ThirtyMinutes => 30,
                KlineInterval.OneHour => 60,
                KlineInterval.TwoHour => 120,
                KlineInterval.FourHour => 240,
                KlineInterval.OneDay => 1440,
                _ => throw new NotSupportedException("Unsupported interval")
            };

            // Calculate time range
            DateTime startTime = endTime.AddMinutes(-200 * intervalMinutes);

            // Loading data
            var result =
                await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime,
                    endTime);

            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Binance API error");
            }
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForBacktestAsync(CurrencyPair currencyPair,
            DateTime startTime, DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
        {
            var result =
                await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime,
                    endTime);

            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Binance API error");
            }
        }

        public int GetIntervalInMinutes(KlineInterval interval)
        {
            return interval switch
            {
                KlineInterval.OneMinute => 1,
                KlineInterval.ThreeMinutes => 3,
                KlineInterval.FiveMinutes => 5,
                KlineInterval.FifteenMinutes => 15,
                KlineInterval.ThirtyMinutes => 30,
                KlineInterval.OneHour => 60,
                KlineInterval.TwoHour => 120,
                KlineInterval.FourHour => 240,
                KlineInterval.SixHour => 360,
                KlineInterval.EightHour => 480,
                KlineInterval.TwelveHour => 720,
                KlineInterval.OneDay => 1440,
                KlineInterval.ThreeDay => 4320,
                KlineInterval.OneWeek => 10080,
                _ => throw new NotSupportedException($"Interval {interval} not supported for minute calculation")
            };
        }

        private TimeSpan GetIntervalTimeSpan(KlineInterval interval)
        {
            return TimeSpan.FromMinutes(GetIntervalInMinutes(interval));
        }
    }
}