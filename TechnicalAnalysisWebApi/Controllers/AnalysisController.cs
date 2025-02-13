using Binance.Net.Enums;
using Microsoft.AspNetCore.Mvc;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.WebApi.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        public AnalysisController(TechnicalAnalysisService technicalAnalysisService)
        {
            _technicalAnalysisService = technicalAnalysisService;
        }

        [HttpGet("Calculate")]
        public async Task<TechnicalAnalysisResult> Calculate(DateTime endTime, CurrencyPair currencyPair, KlineInterval klineInterval)
        {
            BinanceService _binanceService = new BinanceService();
            var data = await _binanceService.GetChartDataForIndicatorsAsync(currencyPair,endTime, klineInterval);
            var result = _technicalAnalysisService.PerformTechnicalAnalysis(data);
            return result;
        }
    }
}
