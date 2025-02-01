﻿using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit;

namespace TraiderAssistant.Infrastructure.Services
{
    public enum CurrencyPair
    {
        BTCUSDT,
        NOTUSDT
    }
    public class BinanceService
    {
        private readonly BinanceRestClient _client;
        private readonly CurrencyPair currency;
        //private readonly MLContext _mlContext;
        //private ITransformer _model;

        /// <summary>
        /// to do избавится от параметров
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>

        public BinanceService(CurrencyPair currencyPair)
        {
            _client = new BinanceRestClient();
            this.currency = currencyPair;
            //_mlContext = new MLContext();
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataAsync(DateTime startTime, DateTime endTime)
        {
            var interval = GetKlineInterval(startTime, endTime);

            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currency.ToString(), interval, startTime, endTime, limit:1000);
            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
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


        public async Task<IEnumerable<BinanceSpotKline>> GetChartDataForIndicatorsAsync(DateTime endTime, KlineInterval interval = KlineInterval.OneMinute)
        {
            // Словарь минимально необходимых данных для каждого индикатора (в интервалах)
            //var requiredPeriods = new Dictionary<string, int>
            //{
            //    { "RSI", 14 }, // Индекс относительной силы
            //    { "Stochastic", 14 }, // Стохастик
            //    { "CCI", 20 }, // Индекс товарного канала
            //    { "ADX", 20 }, // Индикатор среднего направленного движения
            //    { "AwesomeOscillator", 34 }, // Чудесный осциллятор Билла Вильямса
            //    { "Momentum", 10 }, // Моментум
            //    { "MACD", 34 }, // MACD (26 + 9 периодов сигнальной линии)
            //    { "StochasticRSI", 14 }, // Быстрый стохастик RSI
            //    { "WilliamsR", 14 }, // Процентный диапазон Вильямса
            //    { "BullBearPower", 13 }, // Сила быков и медведей
            //    { "UltimateOscillator", 28 } // Окончательный осциллятор
            //};

            // Рассчитать максимальный период из списка индикаторов
            //int maxRequiredPeriods = indicators
            //    .Select(indicator => requiredPeriods.ContainsKey(indicator) ? requiredPeriods[indicator] : 0)
            //    .Max();
            //int maxRequiredPeriods = requiredPeriods.Values.Max();

            // Учитываем коэффициент для текущего таймфрейма
            int intervalMinutes = interval switch
            {
                KlineInterval.OneMinute => 1,
                KlineInterval.FiveMinutes => 5,
                KlineInterval.FifteenMinutes => 15,
                KlineInterval.OneHour => 60,
                KlineInterval.OneDay => 1440,
                _ => throw new NotSupportedException("Unsupported interval")
            };

            // Рассчитать временной диапазон
            //DateTime startTime = endTime.AddMinutes(-maxRequiredPeriods * intervalMinutes);
            DateTime startTime = endTime.AddMinutes(-200 * intervalMinutes);

            // Загрузка данных
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(currency.ToString(), interval, startTime, endTime);

            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new Exception(result.Error.Message);
            }
        }

        //public async Task<IEnumerable<BinanceSpotKline>> GetBitcoinDataAsync(DateTime startTime, DateTime endTime)
        //{
        //    var result = await _client.SpotApi.ExchangeData.GetKlinesAsync("BTCUSDT", KlineInterval.FifteenMinutes, startTime, endTime);
        //    if (result.Success)
        //    {
        //        return result.Data.Cast<BinanceSpotKline>();
        //    }
        //    else
        //    {
        //        throw new Exception(result.Error.Message);
        //    }
        //}

        //public async Task<IEnumerable<BinanceSpotKline>> GetNotcoinDataAsync(DateTime startTime, DateTime endTime)
        //{
        //    var result = await _client.SpotApi.ExchangeData.GetKlinesAsync("NOTUSDT", KlineInterval.FifteenMinutes, startTime, endTime);
        //    if (result.Success)
        //    {
        //        return result.Data.Cast<BinanceSpotKline>();
        //    }
        //    else
        //    {
        //        throw new Exception(result.Error.Message);
        //    }
        //}

    }

    //    public async Task TrainModelAsync(DateTime startTime, DateTime endTime)
    //    {
    //        var historicalData = await GetBitcoinDataAsync(startTime, endTime);
    //        var data = historicalData.Select(kline => new BitcoinData { ClosePrice = (float)kline.ClosePrice }).ToList();

    //        var dataView = _mlContext.Data.LoadFromEnumerable(data);

    //        var pipeline = _mlContext.Forecasting.ForecastBySsa(
    //            outputColumnName: nameof(BitcoinPrediction.Score),
    //            inputColumnName: nameof(BitcoinData.ClosePrice),
    //            windowSize: 7,
    //            seriesLength: data.Count,
    //            trainSize: data.Count,
    //            horizon: 30);

    //        _model = pipeline.Fit(dataView);
    //    }

    //    public float[] PredictBitcoinPrices(int daysToPredict)
    //    {
    //        var forecastEngine = _model.CreateTimeSeriesEngine<BitcoinData, BitcoinPrediction>(_mlContext);
    //        var prediction = forecastEngine.Predict();
    //        return prediction.Score.Take(daysToPredict).ToArray();
    //    }
    //}


    //public class BitcoinData
    //{
    //    public float ClosePrice { get; set; }
    //}

    //public class BitcoinPrediction
    //{
    //    public float[] Score { get; set; }
    //}

}
