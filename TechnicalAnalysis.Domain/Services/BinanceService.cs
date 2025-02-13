using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using System;
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

        public async Task<List<KlineData>> GetChartDataAsync(CurrencyPair currencyPair, DateTime startTime, DateTime endTime)
        {
            var interval = GetKlineInterval(startTime, endTime);

            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime, endTime, limit: 1000);
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
                    //item.CloseTime = item.OpenTime.AddMilliseconds((long)item.Interval);
                }
                return klineDataList;
            }
            else
            {
                throw new Exception(result.Error.Message);
            }
        }
        private KlineInterval GetKlineInterval(DateTime startTime, DateTime endTime)
        {
            TimeSpan duration = endTime - startTime;
            int targetPoints = 400; // Целевое количество точек данных

            double secondsPerCandle = duration.TotalSeconds / targetPoints;

            // Все возможные интервалы с их длительностью в секундах
            var intervals = new Dictionary<KlineInterval, double>
            {
                { KlineInterval.OneSecond, 1 },
                { KlineInterval.OneMinute, 60 },
                { KlineInterval.ThreeMinutes, 180 },
                { KlineInterval.FiveMinutes, 300 },
                { KlineInterval.FifteenMinutes, 900 },
                { KlineInterval.ThirtyMinutes, 1800 },
                { KlineInterval.OneHour, 3600 },
                { KlineInterval.TwoHour, 7200 },
                { KlineInterval.FourHour, 14400 },
                { KlineInterval.SixHour, 21600 },
                { KlineInterval.EightHour, 28800 },
                { KlineInterval.TwelveHour, 43200 },
                { KlineInterval.OneDay, 86400 },
                { KlineInterval.ThreeDay, 259200 },
                { KlineInterval.OneWeek, 604800 },
                { KlineInterval.OneMonth, 2592000 } // Примерное значение месяца (30 дней)
            };

            // Выбираем ближайший больший или равный интервал
            foreach (var interval in intervals)
            {
                if (interval.Value >= secondsPerCandle)
                {
                    return interval.Key;
                }
            }

            // Если ничего не подошло, берем максимальный доступный интервал
            return KlineInterval.OneMonth;
        }


        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForIndicatorsAsync(CurrencyPair currencyPair, DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
        {
            // Учитываем коэффициент для текущего таймфрейма
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
                KlineInterval.OneWeek => 604800,
                KlineInterval.OneMonth => 2592000,

                _ => throw new NotSupportedException("Unsupported interval")
            };

            // Рассчитать временной диапазон
            DateTime startTime = endTime.AddMinutes(-200 * intervalMinutes);

            // Загрузка данных
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currencyPair.ToString(), interval, startTime, endTime);

            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new Exception(result.Error.Message);
            }
        }
    }
}
