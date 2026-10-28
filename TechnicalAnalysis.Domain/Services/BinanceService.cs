using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    
    public class BinanceService
    {
        private readonly BinanceRestClient _client;

        public BinanceService()
        {
            _client = new BinanceRestClient();
        }

        public async Task<List<BinanceSpotKline>> GetFullHistoryDataAsync(CurrencyPair currencyPair,
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

                        if (result.Success && result.Data.Any())
                        {
                            foreach (var kline in result.Data)
                            {
                                allKlines.Add((BinanceSpotKline)kline);
                            }
                        }
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

            // Сортируем и удаляем дубликаты
            return allKlines.GroupBy(x => x.OpenTime)
                            .Select(x => x.First())
                            .OrderBy(x => x.OpenTime)
                            .ToList();
        }

        public async Task<List<KlineData>> GetChartDataAsync(CurrencyPair currencyPair, DateTime startTime, DateTime endTime)
        {
            var interval = GetKlineInterval(startTime, endTime);
            
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime, endTime, limit: 1500);
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

        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForIndicatorsAsync(CurrencyPair currencyPair, DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
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
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime, endTime);

            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Binance API error");
            }
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForBacktestAsync(CurrencyPair currencyPair, DateTime startTime, DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
        {
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime, endTime);

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
