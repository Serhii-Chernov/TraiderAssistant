using Binance.Net;
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

namespace TraiderAssistant.Infrastructure.Services
{
    public class BinanceService
    {
        private readonly BinanceRestClient _client;
        private readonly MLContext _mlContext;
        private ITransformer _model;

        public BinanceService(string apiKey, string apiSecret)
        {
            _client = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            });
            _mlContext = new MLContext();
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetBitcoinDataAsync(DateTime startTime, DateTime endTime)
        {
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync("BTCUSDT", KlineInterval.FifteenMinutes, startTime, endTime);
            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new Exception(result.Error.Message);
            }
        }

        public async Task<IEnumerable<BinanceSpotKline>> GetNotcoinDataAsync(DateTime startTime, DateTime endTime)
        {
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync("NOTUSDT", KlineInterval.FifteenMinutes, startTime, endTime);
            if (result.Success)
            {
                return result.Data.Cast<BinanceSpotKline>();
            }
            else
            {
                throw new Exception(result.Error.Message);
            }
        }

        public async Task TrainModelAsync(DateTime startTime, DateTime endTime)
        {
            var historicalData = await GetBitcoinDataAsync(startTime, endTime);
            var data = historicalData.Select(kline => new BitcoinData { ClosePrice = (float)kline.ClosePrice }).ToList();

            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(BitcoinPrediction.Score),
                inputColumnName: nameof(BitcoinData.ClosePrice),
                windowSize: 7,
                seriesLength: data.Count,
                trainSize: data.Count,
                horizon: 30);

            _model = pipeline.Fit(dataView);
        }

        public float[] PredictBitcoinPrices(int daysToPredict)
        {
            var forecastEngine = _model.CreateTimeSeriesEngine<BitcoinData, BitcoinPrediction>(_mlContext);
            var prediction = forecastEngine.Predict();
            return prediction.Score.Take(daysToPredict).ToArray();
        }
    }


    public class BitcoinData
    {
        public float ClosePrice { get; set; }
    }

    public class BitcoinPrediction
    {
        public float[] Score { get; set; }
    }

}
