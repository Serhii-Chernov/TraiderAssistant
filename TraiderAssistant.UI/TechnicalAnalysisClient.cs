using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TechnicalAnalysis.Shared;


namespace TraiderAssistant.UI
{
    public class TechnicalAnalysisClient
    {
        private ILogger _logger;
        private static readonly HttpClient httpClient = new HttpClient();
        private const string analysisApiUrl = "https://technicalanalysis.azurewebsites.net/Analysis/Calculate";
        private const string analysisLocalUrl = "https://localhost:5001/Analysis/Calculate";//7010
        private const string chartApiUrl = "https://technicalanalysis.azurewebsites.net/Chart/GetChartData";
        private const string chartLocalUrl = "https://localhost:5001/Chart/GetChartData";//7010

        public TechnicalAnalysisClient() 
        {
            
            _logger = SerilogLoggerFactory.CreateLogger<TechnicalAnalysisClient>();
            _logger.LogInfo("TechnicalAnalysisClient initialized");
        }

        public async Task<TechnicalAnalysisResult> GetTechnicalAnalysisAsync(CurrencyPair currencyPair, DateTime endTime, KlineInterval klineInterval)
        {
            try
            {
                string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss");
                //string stringRequest = $"{analysisApiUrl}?currencyPair={(int)currencyPair}&endTime={endTimeStr}&klineInterval={(int)klineInterval}";
                string stringRequest = $"{analysisLocalUrl}?currencyPair={(int)currencyPair}&endTime={endTimeStr}&klineInterval={(int)klineInterval}";
                _logger.LogInfo($"Request sent: {stringRequest}");
                var response = await httpClient.GetAsync(stringRequest);
                _logger.LogInfo($"Response: {response}");
                response.EnsureSuccessStatusCode(); // Throws an exception if the response status is not successful.

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TechnicalAnalysisResult>(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInfo(ex.Message);
            }

            return null;
        }

        public async Task<IEnumerable<KlineData>> GetChartDataAsync(CurrencyPair currencyPair, DateTime startTime, DateTime endTime)
        {
            try
            {
                string startTimeStr = startTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss");

                //string stringRequest = $"{chartApiUrl}?currencyPair={(int)currencyPair}&startTime={startTimeStr}&endTime={endTimeStr}";
                string stringRequest = $"{chartLocalUrl}?currencyPair={(int)currencyPair}&startTime={startTimeStr}&endTime={endTimeStr}";
                _logger.LogInfo($"Request sent: {stringRequest}");

                var response = await httpClient.GetAsync(stringRequest);
                _logger.LogInfo($"Response: {response}");
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var klineDataList = JsonConvert.DeserializeObject<List<KlineData>>(jsonResponse);

                return klineDataList;
            
            }
            catch (JsonSerializationException jsonEx)
            {
                _logger.LogInfo($"JSON Deserialization Error: {jsonEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogInfo($"Request Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogInfo($"Unexpected Error: {ex.Message}");
            }

            return null;

        }
    }
}
