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
        private static readonly HttpClient httpClient = new HttpClient();
        private const string analysisApiUrl = "https://technicalanalysis.azurewebsites.net/Analysis/Calculate";
        private const string chartApiUrl = "https://technicalanalysis.azurewebsites.net/Chart/GetChartData";

        public async Task<TechnicalAnalysisResult> GetTechnicalAnalysisAsync(CurrencyPair currencyPair, DateTime endTime, KlineInterval klineInterval)
        {
            try
            {
                string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string stringRequest = $"{analysisApiUrl}?currencyPair={(int)currencyPair}&endTime={endTimeStr}&klineInterval={(int)klineInterval}";
                
                var response = await httpClient.GetAsync(stringRequest);
                response.EnsureSuccessStatusCode(); // Выбросит исключение, если статус ответа не успешный

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TechnicalAnalysisResult>(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return null;
        }

        public async Task<IEnumerable<KlineData>> GetChartDataAsync(CurrencyPair currencyPair, DateTime startTime, DateTime endTime)
        {
            try
            {
                string startTimeStr = startTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string stringRequest = $"{chartApiUrl}?currencyPair={(int)currencyPair}&startTime={startTimeStr}&endTime={endTimeStr}";

                var response = await httpClient.GetAsync(stringRequest);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var klineDataList = JsonConvert.DeserializeObject<List<KlineData>>(jsonResponse); // Десериализация массива массивов

                return klineDataList;
            
            }
            catch (JsonSerializationException jsonEx)
            {
                MessageBox.Show($"JSON Deserialization Error: {jsonEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($"Request Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected Error: {ex.Message}");
            }

            return null;

        }
    }
}
