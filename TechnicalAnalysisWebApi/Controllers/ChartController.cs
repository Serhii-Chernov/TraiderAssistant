using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChartController : Controller
    {
        [HttpGet("GetChartData")]
        public async Task<IEnumerable<KlineData>> GetChartData(CurrencyPair currencyPair, DateTime startTime, DateTime endTime)
        {
            BinanceService _binanceService = new BinanceService();
            var data = await _binanceService.GetChartDataAsync(currencyPair, startTime, endTime);
            string json = JsonConvert.SerializeObject(data);
            Console.WriteLine(json);

            return data;
        }
    }
}
