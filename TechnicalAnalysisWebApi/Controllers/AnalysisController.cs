using Binance.Net.Enums;
using Microsoft.AspNetCore.Mvc;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.Models;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly BinanceService _binanceService;
        private readonly TournamentSignalService _tournamentSignalService;

        public AnalysisController(
            TechnicalAnalysisService technicalAnalysisService,
            BinanceService binanceService,
            TournamentSignalService tournamentSignalService)
        {
            _technicalAnalysisService = technicalAnalysisService;
            _binanceService = binanceService;
            _tournamentSignalService = tournamentSignalService;
        }

        [HttpGet("Calculate")]
        public async Task<TechnicalAnalysisResult> Calculate(DateTime endTime, CurrencyPair currencyPair, KlineInterval klineInterval)
        {
            // Получаем данные с Binance
            var data = await _binanceService.GetChartDataForIndicatorsAsync(currencyPair, endTime, klineInterval);

            string asset = currencyPair.ToString();
            string timeframe = klineInterval.ToString();

            var result = _technicalAnalysisService.PerformTechnicalAnalysis(data, asset, timeframe);

            return result;
        }

        [HttpGet("TournamentSignal")]
        public async Task<ActionResult<TournamentSignalResult>> TournamentSignal(string asset)
        {
            try
            {
                return Ok(await _tournamentSignalService.GetSignalAsync(asset));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
        }

        [HttpGet("TournamentSignals")]
        public async Task<ActionResult<IReadOnlyList<TournamentSignalResult>>> TournamentSignals() =>
            Ok(await _tournamentSignalService.GetSignalsAsync());
    }
}
