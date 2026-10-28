using Binance.Net.Enums;
using Microsoft.AspNetCore.Mvc;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Shared;
using TechnicalAnalysis.WebApi.Services;

namespace TechnicalAnalysis.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BacktestController : ControllerBase
    {
        private readonly BacktestService _backtestService;

        private readonly IServiceScopeFactory _scopeFactory;

        public BacktestController(BacktestService backtestService, IServiceScopeFactory scopeFactory)
        {
            _backtestService = backtestService;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Запускает одиночный тест стратегии на выбранном таймфрейме.
        /// </summary>
        [HttpGet("Run")]
        public async Task<IActionResult> Run(
            CurrencyPair currencyPair,
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000,
            KlineInterval interval = KlineInterval.OneHour)
        {
            try
            {
                var result =
                    await _backtestService.RunBacktestAsync(currencyPair, startDate, endDate, initialDeposit, interval);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Runs out-of-sample testing for all active database strategies and returns monthly chart-ready results.
        /// </summary>
        [HttpGet("RunForwardTest")]
        public async Task<IActionResult> RunForwardTest(
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000,
            bool useDefaultStrategy = false,
            CurrencyPair? pair = null,
            KlineInterval? interval = null)
        {
            if (endDate <= startDate)
            {
                return BadRequest("End date must be greater than start date.");
            }

            try
            {
                var result = await _backtestService.RunForwardTestAsync(
                    startDate,
                    endDate,
                    initialDeposit,
                    useDefaultStrategy,
                    pair,
                    interval);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Runs an out-of-sample test and returns exact whole-period metrics plus monthly rows.
        /// </summary>
        [HttpGet("RunForwardTestReport")]
        public async Task<IActionResult> RunForwardTestReport(
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000,
            bool useDefaultStrategy = false,
            CurrencyPair? pair = null,
            KlineInterval? interval = null)
        {
            if (endDate <= startDate)
            {
                return BadRequest("End date must be greater than start date.");
            }

            try
            {
                var result = await _backtestService.RunForwardTestReportAsync(
                    startDate,
                    endDate,
                    initialDeposit,
                    useDefaultStrategy,
                    pair,
                    interval);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Runs out-of-sample testing for all active database strategies and returns an XLSX workbook.
        /// </summary>
        [HttpGet("ExportForwardTestXlsx")]
        public async Task<IActionResult> ExportForwardTestXlsx(
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000,
            bool useDefaultStrategy = false,
            CurrencyPair? pair = null,
            KlineInterval? interval = null)
        {
            if (endDate <= startDate)
            {
                return BadRequest("End date must be greater than start date.");
            }

            try
            {
                var result = await _backtestService.RunForwardTestAsync(
                    startDate,
                    endDate,
                    initialDeposit,
                    useDefaultStrategy,
                    pair,
                    interval);
                var workbook = ForwardTestExcelExporter.Export(result);
                var strategyMode = useDefaultStrategy ? "default" : "ml";
                var fileName = $"forward-test_{strategyMode}_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}.xlsx";

                return File(
                    workbook,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Запускает оптимизацию: прогоняет стратегию по ВСЕМ доступным таймфреймам (1m, 5m, 1h, 4h, 1d)
        /// и возвращает результаты для сравнения.
        /// </summary>
        [HttpGet("RunOptimization")]
        public async Task<IActionResult> RunOptimization(
            CurrencyPair currencyPair,
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000)
        {
            try
            {
                // Вызываем метод оптимизации, добавленный в сервис на предыдущем шаге
                var results =
                    await _backtestService.RunOptimizationAsync(currencyPair, startDate, endDate, initialDeposit);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("RunGlobalOptimization")]
        public async Task<IActionResult> RunGlobalOptimization(
            [FromQuery] CurrencyPair[] pairs,
            DateTime startDate,
            DateTime endDate,
            decimal initialDeposit = 1000)
        {
            var pairsToTest = NormalizePairs(pairs);
            if (pairsToTest.Length == 0)
            {
                return BadRequest("Укажите хотя бы одну валютную пару в параметре pairs.");
            }

            if (endDate <= startDate)
            {
                return BadRequest("End date must be greater than start date.");
            }

            if (initialDeposit <= 0)
            {
                return BadRequest("Initial deposit must be greater than zero.");
            }

            await Task.WhenAll(pairsToTest.Select(RunPairOptimizationAsync));

            return Ok($"Глобальная оптимизация завершена для {pairsToTest.Length} валютных пар.");

            async Task RunPairOptimizationAsync(CurrencyPair pair)
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedBacktestService = scope.ServiceProvider.GetRequiredService<BacktestService>();
                await scopedBacktestService.RunOptimizationAsync(pair, startDate, endDate, initialDeposit);
            }
        }

        internal static CurrencyPair[] NormalizePairs(IEnumerable<CurrencyPair> pairs) =>
            pairs?.Distinct().ToArray() ?? Array.Empty<CurrencyPair>();

        [HttpGet("TrainModel")]
        public async Task<IActionResult> TrainModel()
        {
            var mlService = HttpContext.RequestServices.GetRequiredService<MachineLearningService>();
            // Тепер метод TrainModelAsync аналізує відразу всю базу
            var result = await mlService.TrainModelAsync();
            return Ok(result);
        }

        [HttpGet("PredictBestStrategies")] // Название эндпоинта можно сделать во множественном числе
        public async Task<IActionResult> PredictBestStrategies()
        {
            try
            {
                var mlService = HttpContext.RequestServices.GetRequiredService<MachineLearningService>();
                // Вызываем новый метод без параметров
                var result = await mlService.FindBestStrategiesForAllTimeframes();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("RunSingle")]
        public async Task<IActionResult> RunSingle(string pair, string interval, decimal initialDeposit = 1000)
        {
            try
            {
                if (!Enum.TryParse<CurrencyPair>(pair, out var parsedPair)) return BadRequest("Неверная валютная пара");
                if (!Enum.TryParse<KlineInterval>(interval, out var parsedInterval))
                    return BadRequest("Неверный таймфрейм");

                // Даты, на которых обучался робот (чтобы показать результаты комиссии)
                DateTime startDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime endDate = DateTime.UtcNow;

                var result =
                    await _backtestService.RunAITestAsync(parsedPair, parsedInterval, startDate, endDate,
                        initialDeposit);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}