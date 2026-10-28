using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TechnicalAnalysis.Domain.Services.Backtesting;
using TechnicalAnalysis.Shared;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services
{
    public class BacktestService
    {
        private readonly BinanceService _binanceService;
        private readonly TechnicalAnalysisService _analysisService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<BacktestService> _logger;
        private const decimal TradingFee = 0.001m;
        private const int WarmUpCandlesCount = 300;
        internal const int TargetOptimizationCombinationsPerInterval = 200;

        public BacktestService(BinanceService binanceService, TechnicalAnalysisService analysisService, AppDbContext dbContext, ILogger<BacktestService> logger)
        {
            _binanceService = binanceService;
            _analysisService = analysisService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Runs out-of-sample testing for all active strategies stored in the database and groups closed trades by month.
        /// </summary>
        /// <returns>Monthly forward-test results grouped by asset and timeframe.</returns>
        public async Task<List<ForwardTestMonthlyResult>> RunForwardTestAsync(DateTime startTime, DateTime endTime, decimal initialDeposit, bool useDefaultStrategy = false)
        {
            ValidateBacktestRequest(startTime, endTime, initialDeposit);
            _logger.LogInformation("Forward test started. StartTime: {StartTime}, EndTime: {EndTime}, InitialDeposit: {InitialDeposit}, UseDefaultStrategy: {UseDefaultStrategy}", startTime, endTime, initialDeposit, useDefaultStrategy);

            var reports = new List<ForwardTestMonthlyResult>();

            var activeStrategies = await _dbContext.ActiveStrategies.ToListAsync();
            _logger.LogInformation("Loaded {StrategyCount} active strategies for forward test.", activeStrategies.Count);

            foreach (var strategy in activeStrategies)
            {
                if (!Enum.TryParse<CurrencyPair>(strategy.Asset, out var pair) ||
                    !Enum.TryParse<KlineInterval>(strategy.Timeframe, out var timeframe))
                {
                    _logger.LogWarning("Skipping active strategy {StrategyId}: cannot parse asset {Asset} or timeframe {Timeframe}.", strategy.Id, strategy.Asset, strategy.Timeframe);
                    continue;
                }

                BacktestResult result;
                try
                {
                    _logger.LogInformation("Forward test strategy started. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}", strategy.Id, strategy.Asset, strategy.Timeframe);
                    result = await RunActiveStrategyAsync(strategy, pair, timeframe, startTime, endTime, initialDeposit, useDefaultStrategy);
                    _logger.LogInformation("Forward test strategy finished. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, Trades: {Trades}", strategy.Id, strategy.Asset, strategy.Timeframe, result.TotalTrades);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Forward test strategy failed. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}", strategy.Id, strategy.Asset, strategy.Timeframe);
                    continue;
                }

                var monthlyGroups = result.Trades
                    .Where(t => t.ExitTime.HasValue)
                    .GroupBy(t => t.ExitTime.Value.ToString("yyyy-MM"))
                    .OrderBy(g => g.Key)
                    .ToList();

                _logger.LogInformation("Grouped strategy results by month. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, MonthGroups: {MonthGroups}", strategy.Id, strategy.Asset, strategy.Timeframe, monthlyGroups.Count);

                foreach (var group in monthlyGroups)
                {
                    int totalTrades = group.Count();
                    int winTrades = group.Count(t => t.Profit > 0);
                    decimal winRate = totalTrades > 0 ? (decimal)winTrades / totalTrades * 100 : 0;
                    decimal netProfit = BacktestEngine.CalculateCompoundedReturn(group);
                    decimal maxDrawdown = BacktestEngine.CalculateTradeMaxDrawdown(group);

                    reports.Add(new ForwardTestMonthlyResult
                    {
                        Month = group.Key,
                        CurrencyPair = pair.ToString(),
                        Timeframe = timeframe.ToString(),
                        TotalTrades = totalTrades,
                        WinRate = Math.Round(winRate, 2),
                        MaxDrawdown = Math.Round(maxDrawdown, 2),
                        NetProfit = Math.Round(netProfit, 2)
                    });
                }
            }

            var groupedReports = reports
                .GroupBy(r => new { r.Month, r.CurrencyPair, r.Timeframe })
                .Select(g =>
                {
                    int totalTrades = g.Sum(r => r.TotalTrades);

                    return new ForwardTestMonthlyResult
                    {
                        Month = g.Key.Month,
                        CurrencyPair = g.Key.CurrencyPair,
                        Timeframe = g.Key.Timeframe,
                        TotalTrades = totalTrades,
                        WinRate = totalTrades > 0 ? Math.Round(g.Sum(r => r.WinRate * r.TotalTrades) / totalTrades, 2) : 0,
                        MaxDrawdown = Math.Round(g.Max(r => r.MaxDrawdown), 2),
                        NetProfit = Math.Round(g.Sum(r => r.NetProfit), 2)
                    };
                })
                .OrderBy(r => r.Month)
                .ThenBy(r => r.CurrencyPair)
                .ThenBy(r => r.Timeframe)
                .ToList();

            _logger.LogInformation("Forward test finished. ResultRows: {ResultRows}", groupedReports.Count);

            return groupedReports;
        }

        private async Task<BacktestResult> RunActiveStrategyAsync(
            ActiveStrategy strategy,
            CurrencyPair pair,
            KlineInterval interval,
            DateTime startTime,
            DateTime endTime,
            decimal initialDeposit,
            bool useDefaultStrategy)
        {
            _logger.LogInformation("Loading historical candles. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, StartTime: {StartTime}, EndTime: {EndTime}", strategy.Id, strategy.Asset, strategy.Timeframe, startTime, endTime);

            var config = useDefaultStrategy ? null : JsonConvert.DeserializeObject<WeightsJsonSchema>(strategy.IndicatorWeightsJson);

            int intervalMinutes = _binanceService.GetIntervalInMinutes(interval);
            DateTime warmUpStartTime = startTime.AddMinutes(-1 * WarmUpCandlesCount * intervalMinutes);

            var rawData = await _binanceService.GetFullHistoryDataAsync(pair, interval, warmUpStartTime, endTime);
            var historyData = rawData.ToList();
            _logger.LogInformation("Historical candles loaded. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, Candles: {CandleCount}", strategy.Id, strategy.Asset, strategy.Timeframe, historyData.Count);

            int simulationStartIndex = GetSimulationStartIndex(historyData, startTime);
            var oscKeys = GetOscillatorKeys();
            var maKeys = GetMovingAverageKeys();

            _logger.LogInformation("Indicator pre-calculation started. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, SimulationStartIndex: {SimulationStartIndex}, Candles: {CandleCount}", strategy.Id, strategy.Asset, strategy.Timeframe, simulationStartIndex, historyData.Count);
            var fastCache = BuildCandleSnapshots(historyData, simulationStartIndex, oscKeys, maKeys);
            _logger.LogInformation("Indicator pre-calculation finished. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}", strategy.Id, strategy.Asset, strategy.Timeframe);

            decimal[] oscWeightsArr = new decimal[11];
            decimal[] maWeightsArr = new decimal[13];
            var oscWeights = useDefaultStrategy ? GetDefaultOscillatorWeights() : config?.Oscillators ?? GetDefaultOscillatorWeights();
            var maWeights = useDefaultStrategy ? GetDefaultMovingAverageWeights() : config?.MAs ?? GetDefaultMovingAverageWeights();

            for (int j = 0; j < 11; j++) oscWeightsArr[j] = oscWeights.GetValueOrDefault(oscKeys[j], 0);
            for (int j = 0; j < 13; j++) maWeightsArr[j] = maWeights.GetValueOrDefault(maKeys[j], 0);

            decimal oscGroupWeight = 0.5m;
            decimal maGroupWeight = 0.5m;
            int buyThreshold = 30;
            int sellThreshold = -30;
            decimal stopLoss = 0.05m;
            decimal takeProfit = 0.10m;

            if (!useDefaultStrategy)
            {
                if (config?.OscillatorGroupWeight > 0) oscGroupWeight = config.OscillatorGroupWeight;
                if (config?.MaGroupWeight > 0) maGroupWeight = config.MaGroupWeight;
                if (strategy.BuyThreshold != 0) buyThreshold = strategy.BuyThreshold;
                else if (config != null) buyThreshold = config.BuyThreshold;
                if (strategy.SellThreshold != 0) sellThreshold = strategy.SellThreshold;
                else if (config != null) sellThreshold = config.SellThreshold;
                if (strategy.StopLoss > 0) stopLoss = strategy.StopLoss;
                else if (config?.StopLoss > 0) stopLoss = config.StopLoss;
                if (strategy.TakeProfit > 0) takeProfit = strategy.TakeProfit;
                else if (config?.TakeProfit > 0) takeProfit = config.TakeProfit;
            }

            _logger.LogInformation("Simulation started. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, UseDefaultStrategy: {UseDefaultStrategy}", strategy.Id, strategy.Asset, strategy.Timeframe, useDefaultStrategy);

            var result = BacktestEngine.Run(
                fastCache,
                simulationStartIndex,
                initialDeposit,
                CreateBacktestSettings(
                    oscWeightsArr,
                    maWeightsArr,
                    oscGroupWeight,
                    maGroupWeight,
                    buyThreshold,
                    sellThreshold,
                    stopLoss,
                    takeProfit),
                trackTrades: true);

            _logger.LogInformation("Simulation finished. StrategyId: {StrategyId}, Asset: {Asset}, Timeframe: {Timeframe}, FinalBalance: {FinalBalance}, ProfitPercentage: {ProfitPercentage}, Trades: {Trades}", strategy.Id, strategy.Asset, strategy.Timeframe, result.FinalBalance, result.ProfitPercentage, result.TotalTrades);

            return result;
        }

        /// <summary>
        /// Запускает боевой тест стратегии ИИ из базы данных (ActiveStrategies) для построения графика в UI
        /// </summary>
        public async Task<BacktestResult> RunAITestAsync(CurrencyPair pair, KlineInterval interval, DateTime startTime, DateTime endTime, decimal initialDeposit)
        {
            ValidateBacktestRequest(startTime, endTime, initialDeposit);

            // 1. Ищем боевую стратегию ИИ в БД
            var strategy = await _dbContext.ActiveStrategies
                .FirstOrDefaultAsync(s => s.Asset == pair.ToString() && s.Timeframe == interval.ToString());

            if (strategy == null)
                throw new InvalidOperationException($"Нет обученной ИИ-стратегии для {pair} {interval}. Сначала запустите глобальную оптимизацию.");

            var config = JsonConvert.DeserializeObject<WeightsJsonSchema>(strategy.IndicatorWeightsJson);

            // 2. Скачиваем свечи с Binance
            int intervalMinutes = _binanceService.GetIntervalInMinutes(interval);
            DateTime warmUpStartTime = startTime.AddMinutes(-1 * WarmUpCandlesCount * intervalMinutes);

            var rawData = await _binanceService.GetFullHistoryDataAsync(pair, interval, warmUpStartTime, endTime);
            var historyData = rawData.ToList();

            int simulationStartIndex = GetSimulationStartIndex(historyData, startTime);

            // 3. Предрасчет (Кэширование индикаторов)
            var oscKeys = GetOscillatorKeys();
            var maKeys = GetMovingAverageKeys();
            var fastCache = BuildCandleSnapshots(historyData, simulationStartIndex, oscKeys, maKeys);

            // 4. Подставляем точные веса ИИ
            decimal[] oscWeightsArr = new decimal[11];
            decimal[] maWeightsArr = new decimal[13];
            for (int j = 0; j < 11; j++) oscWeightsArr[j] = config.Oscillators.GetValueOrDefault(oscKeys[j], 0);
            for (int j = 0; j < 13; j++) maWeightsArr[j] = config.MAs.GetValueOrDefault(maKeys[j], 0);

            decimal oscGroupWeight = config.OscillatorGroupWeight > 0 ? config.OscillatorGroupWeight : 0.5m;
            decimal maGroupWeight = config.MaGroupWeight > 0 ? config.MaGroupWeight : 0.5m;

            // 5. Запуск симуляции с флагом trackTrades = true (чтобы нарисовать график)
            var result = BacktestEngine.Run(
                fastCache,
                simulationStartIndex,
                initialDeposit,
                CreateBacktestSettings(
                    oscWeightsArr,
                    maWeightsArr,
                    oscGroupWeight,
                    maGroupWeight,
                    config.BuyThreshold,
                    config.SellThreshold,
                    config.StopLoss,
                    config.TakeProfit),
                trackTrades: true);

            return result;
        }

        public async Task<List<BacktestResult>> RunOptimizationAsync(CurrencyPair pair, DateTime startDate, DateTime endDate, decimal initialDeposit)
        {
            ValidateBacktestRequest(startDate, endDate, initialDeposit);

            var results = new List<BacktestResult>();

            var existingRuns = await _dbContext.OptimizationRuns
                .Select(r => new { r.Asset, r.Timeframe, r.IndicatorWeightsJson })
                .ToListAsync();

            var testedCombinations = new ConcurrentDictionary<string, byte>();
            foreach (var run in existingRuns)
            {
                testedCombinations.TryAdd(
                    GetOptimizationCombinationKey(run.Asset, run.Timeframe, run.IndicatorWeightsJson),
                    1);
            }

            var intervalsToCheck = new List<KlineInterval>
            {
                KlineInterval.OneMinute, KlineInterval.FiveMinutes, KlineInterval.FifteenMinutes,
                KlineInterval.ThirtyMinutes, KlineInterval.OneHour, KlineInterval.FourHour, KlineInterval.OneDay
            };

            var oscKeys = GetOscillatorKeys();
            var maKeys = GetMovingAverageKeys();

            foreach (var interval in intervalsToCheck)
            {
                var asset = pair.ToString();
                var timeframe = interval.ToString();
                var existingCombinationCount = existingRuns
                    .Where(run => run.Asset == asset && run.Timeframe == timeframe)
                    .Select(run => run.IndicatorWeightsJson)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var combinationsToTest = CalculateRemainingOptimizationCombinations(existingCombinationCount);

                if (combinationsToTest == 0)
                {
                    _logger.LogInformation(
                        "Optimization skipped for {Pair} {Interval}: target of {TargetCount} combinations is already reached.",
                        pair,
                        interval,
                        TargetOptimizationCombinationsPerInterval);
                    continue;
                }

                _logger.LogInformation(
                    "Optimization will resume for {Pair} {Interval}. Existing: {ExistingCount}, Remaining: {RemainingCount}, Target: {TargetCount}.",
                    pair,
                    interval,
                    existingCombinationCount,
                    combinationsToTest,
                    TargetOptimizationCombinationsPerInterval);

                try
                {
                    int intervalMinutes = _binanceService.GetIntervalInMinutes(interval);
                    DateTime warmUpStartTime = startDate.AddMinutes(-1 * WarmUpCandlesCount * intervalMinutes);

                    Console.WriteLine($"Fetching data for {interval}...");
                    var rawData = await _binanceService.GetFullHistoryDataAsync(pair, interval, warmUpStartTime, endDate);
                    var historyData = rawData.ToList();

                    if (historyData.Count < WarmUpCandlesCount)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient history for {pair} {interval}: received {historyData.Count} candles, " +
                            $"but at least {WarmUpCandlesCount} are required.");
                    }

                    int simulationStartIndex = GetSimulationStartIndex(historyData, startDate);

                    // === 1. ПРЕДРАСЧЕТ ИНДИКАТОРОВ (КЭШИРОВАНИЕ) ===
                    Console.WriteLine($"[{pair}] Pre-calculating indicators for {historyData.Count - simulationStartIndex} candles...");
                    var fastCache = BuildCandleSnapshots(historyData, simulationStartIndex, oscKeys, maKeys);
                    Console.WriteLine($"[{pair}] ({interval}) Pre-calculation FINISHED!");

                    // === 2. СВЕРХБЫСТРОЕ ПАРАЛЛЕЛЬНОЕ ТЕСТИРОВАНИЕ КОМБИНАЦИЙ ===
                    Console.WriteLine($"Testing {combinationsToTest} remaining combinations in parallel for {interval}...");
                    var runsToSave = new ConcurrentBag<OptimizationRun>();
                    var concurrentResults = new ConcurrentBag<BacktestResult>();

                    Parallel.For(0, combinationsToTest, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 4) }, i =>
                    {
                        var rnd = new Random(Guid.NewGuid().GetHashCode());
                        string combinedWeightsJson;
                        var candidateOrdinal = existingCombinationCount + i;

                        decimal[] oscWeightsArr = new decimal[11];
                        decimal[] maWeightsArr = new decimal[13];
                        int buyThreshold, sellThreshold;
                        decimal stopLossPct, takeProfitPct;
                        decimal oscGroupWeight, maGroupWeight;

                        while (true)
                        {
                            Dictionary<string, decimal> oscDict;
                            Dictionary<string, decimal> maDict;

                            if (candidateOrdinal == 0)
                            {
                                // ЕТАЛОН 1: Класичний трейдинг (людська логіка)
                                oscDict = GetDefaultOscillatorWeights();
                                maDict = GetDefaultMovingAverageWeights();
                                buyThreshold = 30;
                                sellThreshold = -30;
                                stopLossPct = 0.05m;
                                takeProfitPct = 0.10m;
                                oscGroupWeight = 0.5m;
                                maGroupWeight = 0.5m;
                            }
                            else if (candidateOrdinal == 1)
                            {
                                // ЕТАЛОН 2: Динамічний Победитель від ІА
                                var aiBenchmark = GetAIBenchmark(interval);
                                oscDict = aiBenchmark.OscWeights;
                                maDict = aiBenchmark.MaWeights;
                                buyThreshold = aiBenchmark.BuyThreshold;
                                sellThreshold = aiBenchmark.SellThreshold;
                                stopLossPct = aiBenchmark.StopLoss;
                                takeProfitPct = aiBenchmark.TakeProfit;
                                oscGroupWeight = 0.5m; // Дефолт для старых эталонов
                                maGroupWeight = 0.5m;
                            }
                            else
                            {
                                // СЛУЧАЙНАЯ ГЕНЕРАЦИЯ (ДЛЯ ML)
                                oscDict = GenerateRandomWeights(oscKeys, rnd);
                                maDict = GenerateRandomWeights(maKeys, rnd);
                                buyThreshold = rnd.Next(10, 60);
                                sellThreshold = rnd.Next(-60, -10);
                                stopLossPct = (decimal)(rnd.NextDouble() * 0.04 + 0.01);
                                takeProfitPct = (decimal)(rnd.NextDouble() * 0.13 + 0.02);
                                oscGroupWeight = (decimal)rnd.NextDouble();
                                maGroupWeight = 1.0m - oscGroupWeight;
                            }

                            for (int j = 0; j < 11; j++) oscWeightsArr[j] = oscDict[oscKeys[j]];
                            for (int j = 0; j < 13; j++) maWeightsArr[j] = maDict[maKeys[j]];

                            var combined = new
                            {
                                OscillatorGroupWeight = oscGroupWeight,
                                MaGroupWeight = maGroupWeight,
                                Oscillators = oscDict,
                                MAs = maDict,
                                BuyThreshold = buyThreshold,
                                SellThreshold = sellThreshold,
                                StopLoss = stopLossPct,
                                TakeProfit = takeProfitPct
                            };
                            combinedWeightsJson = JsonConvert.SerializeObject(combined);

                            var combinationKey = GetOptimizationCombinationKey(
                                pair.ToString(),
                                interval.ToString(),
                                combinedWeightsJson);

                            if (testedCombinations.TryAdd(combinationKey, 1))
                            {
                                break;
                            }

                            // A benchmark may already exist in an unusual partial dataset.
                            // Fall back to a random candidate so the target count is still reached.
                            candidateOrdinal = 2;
                        }

                        // Запускаем симуляцию
                        var result = BacktestEngine.Run(
                            fastCache,
                            simulationStartIndex,
                            initialDeposit,
                            CreateBacktestSettings(
                                oscWeightsArr,
                                maWeightsArr,
                                oscGroupWeight,
                                maGroupWeight,
                                buyThreshold,
                                sellThreshold,
                                stopLossPct,
                                takeProfitPct),
                            trackTrades: false);

                        var runRecord = new OptimizationRun
                        {
                            Timeframe = interval.ToString(),
                            Asset = pair.ToString(),
                            IndicatorWeightsJson = combinedWeightsJson,
                            TotalProfitPercent = result.ProfitPercentage,
                            TotalTrades = result.TotalTrades,
                            WinningTrades = result.WinningTrades,
                            MaxDrawdown = result.MaxDrawdown,
                            RunDate = DateTime.UtcNow
                        };

                        runsToSave.Add(runRecord);
                        concurrentResults.Add(result);
                    });

                    // === 3. МАССОВОЕ СОХРАНЕНИЕ ===
                    _dbContext.OptimizationRuns.AddRange(runsToSave);
                    await _dbContext.SaveChangesAsync();
                    _dbContext.ChangeTracker.Clear();

                    results.AddRange(concurrentResults);
                    _logger.LogInformation(
                        "Optimization completed for {Pair} {Interval}. Added: {AddedCount}, Total: {TotalCount}/{TargetCount}.",
                        pair,
                        interval,
                        runsToSave.Count,
                        existingCombinationCount + runsToSave.Count,
                        TargetOptimizationCombinationsPerInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Optimization failed for {Pair} {Interval}.", pair, interval);
                    throw new InvalidOperationException($"Optimization failed for {pair} {interval}.", ex);
                }
            }

            return results;
        }

        internal static string GetOptimizationCombinationKey(string asset, string timeframe, string weightsJson) =>
            $"{asset}\u001F{timeframe}\u001F{weightsJson}";

        internal static int CalculateRemainingOptimizationCombinations(
            int existingCombinationCount,
            int targetCombinationCount = TargetOptimizationCombinationsPerInterval)
        {
            if (existingCombinationCount < 0)
                throw new ArgumentOutOfRangeException(nameof(existingCombinationCount));
            if (targetCombinationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetCombinationCount));

            return Math.Max(0, targetCombinationCount - existingCombinationCount);
        }

        // Одиночный запуск (старый)
        public async Task<BacktestResult> RunBacktestAsync(CurrencyPair pair, DateTime startTime, DateTime endTime, decimal initialDeposit, KlineInterval interval)
        {
            ValidateBacktestRequest(startTime, endTime, initialDeposit);

            int intervalMinutes = _binanceService.GetIntervalInMinutes(interval);
            DateTime warmUpStartTime = startTime.AddMinutes(-1 * WarmUpCandlesCount * intervalMinutes);

            var rawData = await _binanceService.GetFullHistoryDataAsync(pair, interval, warmUpStartTime, endTime);
            var historyData = rawData.ToList();

            int simulationStartIndex = GetSimulationStartIndex(historyData, startTime);

            var oscKeys = GetOscillatorKeys();
            var maKeys = GetMovingAverageKeys();
            var fastCache = BuildCandleSnapshots(historyData, simulationStartIndex, oscKeys, maKeys);

            var defaultOscWeights = GetDefaultOscillatorWeights();
            var defaultMaWeights = GetDefaultMovingAverageWeights();
            decimal[] oscWeightsArr = new decimal[11];
            decimal[] maWeightsArr = new decimal[13];
            for (int j = 0; j < 11; j++) oscWeightsArr[j] = defaultOscWeights[oscKeys[j]];
            for (int j = 0; j < 13; j++) maWeightsArr[j] = defaultMaWeights[maKeys[j]];

            var result = BacktestEngine.Run(
                fastCache,
                simulationStartIndex,
                initialDeposit,
                CreateBacktestSettings(
                    oscWeightsArr,
                    maWeightsArr,
                    0.5m,
                    0.5m,
                    30,
                    -30,
                    0.05m,
                    0.10m),
                trackTrades: true);

            return result;
        }

        // ================= ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ =================

        private BacktestCandleSnapshot[] BuildCandleSnapshots(
            List<BinanceSpotKline> historyData,
            int simulationStartIndex,
            IReadOnlyList<string> oscillatorKeys,
            IReadOnlyList<string> movingAverageKeys)
        {
            var snapshots = historyData.Select(kline => new BacktestCandleSnapshot
            {
                OpenTime = kline.OpenTime,
                OpenPrice = kline.OpenPrice,
                HighPrice = kline.HighPrice,
                LowPrice = kline.LowPrice,
                ClosePrice = kline.ClosePrice
            }).ToArray();

            // The final candle is reserved for execution/forced close because it may still be incomplete.
            for (int index = simulationStartIndex; index < historyData.Count - 1; index++)
            {
                var analysisWindow = historyData.GetRange(index - 250, 251);
                var analysisResult = _analysisService.PerformTechnicalAnalysis(analysisWindow);

                snapshots[index].OscillatorActions = oscillatorKeys
                    .Select(key => ToActionValue(
                        analysisResult.Oscillators.Find(item => item.Name == key)?.Action,
                        key))
                    .ToArray();
                snapshots[index].MovingAverageActions = movingAverageKeys
                    .Select(key => ToActionValue(
                        analysisResult.MovingAverages.Find(item => item.Name == key)?.Action,
                        key))
                    .ToArray();

                if (index % 5000 == 0)
                {
                    _logger.LogInformation(
                        "Indicator pre-calculation progress. Processed: {Processed}, Total: {Total}",
                        index,
                        historyData.Count);
                }
            }

            return snapshots;
        }

        private static decimal ToActionValue(string action, string indicatorName)
        {
            return action switch
            {
                TradeAction.Buy => 1m,
                TradeAction.Sell => -1m,
                TradeAction.Neutral => 0m,
                null => throw new InvalidOperationException($"Indicator '{indicatorName}' is missing from analysis result."),
                _ => 0m
            };
        }

        private static int GetSimulationStartIndex(
            IReadOnlyList<BinanceSpotKline> historyData,
            DateTime startTime)
        {
            int simulationStartIndex = -1;
            for (int index = 0; index < historyData.Count; index++)
            {
                if (historyData[index].OpenTime >= startTime)
                {
                    simulationStartIndex = index;
                    break;
                }
            }

            if (simulationStartIndex < 0)
                throw new InvalidOperationException("Historical data does not contain the requested start time.");
            if (simulationStartIndex < 250)
                throw new InvalidOperationException("At least 250 warm-up candles are required before the simulation start.");
            if (simulationStartIndex >= historyData.Count - 1)
                throw new InvalidOperationException("Historical data does not contain candles after the simulation start.");

            return simulationStartIndex;
        }

        private static BacktestSettings CreateBacktestSettings(
            decimal[] oscillatorWeights,
            decimal[] movingAverageWeights,
            decimal oscillatorGroupWeight,
            decimal movingAverageGroupWeight,
            int buyThreshold,
            int sellThreshold,
            decimal stopLoss,
            decimal takeProfit)
        {
            return new BacktestSettings(
                oscillatorWeights,
                movingAverageWeights,
                oscillatorGroupWeight,
                movingAverageGroupWeight,
                buyThreshold,
                sellThreshold,
                stopLoss,
                takeProfit,
                TradingFee);
        }

        private static void ValidateBacktestRequest(
            DateTime startTime,
            DateTime endTime,
            decimal initialDeposit)
        {
            if (endTime <= startTime)
                throw new ArgumentException("End time must be greater than start time.", nameof(endTime));
            if (initialDeposit <= 0m)
                throw new ArgumentOutOfRangeException(nameof(initialDeposit), "Initial deposit must be positive.");
        }

        private void NormalizeWeights(Dictionary<string, decimal> weights)
        {
            decimal sum = weights.Values.Sum();
            if (sum > 0)
            {
                foreach (var key in weights.Keys.ToList())
                {
                    weights[key] = weights[key] / sum;
                }
            }
        }

        private (Dictionary<string, decimal> OscWeights, Dictionary<string, decimal> MaWeights, int BuyThreshold, int SellThreshold, decimal StopLoss, decimal TakeProfit) GetAIBenchmark(KlineInterval interval)
        {
            var oscWeights = GetDefaultOscillatorWeights();
            var maWeights = GetDefaultMovingAverageWeights();

            int buyThresh = 30, sellThresh = -30;
            decimal sl = 0.05m, tp = 0.10m;

            switch (interval)
            {
                case KlineInterval.FourHour:
                    buyThresh = 58; sellThresh = -11; sl = 0.0225m; tp = 0.1341m;
                    oscWeights["RSI"] = 0.223m; oscWeights["MACD"] = 0.036m; oscWeights["CCI"] = 0.150m;
                    maWeights["SMA(200)"] = 0.001m; maWeights["EMA(200)"] = 0.126m; maWeights["HMA(9)"] = 0.026m;
                    break;
                case KlineInterval.OneDay:
                    buyThresh = 10; sellThresh = -46; sl = 0.0440m; tp = 0.1084m;
                    oscWeights["RSI"] = 0.150m; oscWeights["MACD"] = 0.153m; oscWeights["CCI"] = 0.034m;
                    maWeights["SMA(200)"] = 0.188m; maWeights["EMA(200)"] = 0.042m; maWeights["HMA(9)"] = 0.051m;
                    break;
                case KlineInterval.ThirtyMinutes:
                    buyThresh = 58; sellThresh = -57; sl = 0.0295m; tp = 0.1245m;
                    oscWeights["RSI"] = 0.103m; oscWeights["MACD"] = 0.133m; oscWeights["CCI"] = 0.153m;
                    maWeights["SMA(200)"] = 0.004m; maWeights["EMA(200)"] = 0.091m; maWeights["HMA(9)"] = 0.082m;
                    break;
                case KlineInterval.OneHour:
                    buyThresh = 58; sellThresh = -48; sl = 0.0165m; tp = 0.0229m;
                    oscWeights["RSI"] = 0.107m; oscWeights["MACD"] = 0.058m; oscWeights["CCI"] = 0.124m;
                    maWeights["SMA(200)"] = 0.092m; maWeights["EMA(200)"] = 0.001m; maWeights["HMA(9)"] = 0.123m;
                    break;
                case KlineInterval.FifteenMinutes:
                    buyThresh = 55; sellThresh = -47; sl = 0.0414m; tp = 0.1461m;
                    oscWeights["RSI"] = 0.162m; oscWeights["MACD"] = 0.003m; oscWeights["CCI"] = 0.161m;
                    maWeights["SMA(200)"] = 0.069m; maWeights["EMA(200)"] = 0.200m; maWeights["HMA(9)"] = 0.060m;
                    break;
                case KlineInterval.FiveMinutes:
                    buyThresh = 53; sellThresh = -35; sl = 0.0392m; tp = 0.0419m;
                    oscWeights["RSI"] = 0.231m; oscWeights["MACD"] = 0.025m; oscWeights["CCI"] = 0.131m;
                    maWeights["SMA(200)"] = 0.081m; maWeights["EMA(200)"] = 0.155m; maWeights["HMA(9)"] = 0.151m;
                    break;
                case KlineInterval.OneMinute:
                    buyThresh = 59; sellThresh = -52; sl = 0.0271m; tp = 0.1039m;
                    oscWeights["RSI"] = 0.001m; oscWeights["MACD"] = 0.070m; oscWeights["CCI"] = 0.035m;
                    maWeights["SMA(200)"] = 0.107m; maWeights["EMA(200)"] = 0.074m; maWeights["HMA(9)"] = 0.116m;
                    break;
            }

            NormalizeWeights(oscWeights);
            NormalizeWeights(maWeights);

            return (oscWeights, maWeights, buyThresh, sellThresh, sl, tp);
        }

        private Dictionary<string, decimal> GenerateRandomWeights(string[] keys, Random rnd)
        {
            var rawWeights = keys.Select(k => new { Key = k, Value = (decimal)rnd.NextDouble() }).ToList();
            decimal sum = rawWeights.Sum(x => x.Value);
            return rawWeights.ToDictionary(x => x.Key, x => x.Value / (sum == 0 ? 1 : sum));
        }

        private string[] GetOscillatorKeys() => new[] { "RSI", "StochasticK", "CCI", "ADX", "AO", "Momentum", "MACD", "UltimateOscillator", "Williams%R", "BullBearPower", "StochasticRSI" };
        private string[] GetMovingAverageKeys() => new[] { "SMA(10)", "EMA(10)", "SMA(20)", "EMA(20)", "SMA(30)", "EMA(30)", "SMA(50)", "EMA(50)", "SMA(100)", "EMA(100)", "SMA(200)", "EMA(200)", "HMA(9)" };

        private Dictionary<string, decimal> GetDefaultOscillatorWeights() => new Dictionary<string, decimal>
        {
            { "RSI", 0.15m }, { "StochasticK", 0.1m }, { "CCI", 0.1m }, { "ADX", 0.15m },
            { "AO", 0.1m }, { "Momentum", 0.1m }, { "MACD", 0.1m }, { "UltimateOscillator", 0.1m },
            { "Williams%R", 0.1m }, { "BullBearPower", 0.1m }, { "StochasticRSI", 0.1m }
        };

        private Dictionary<string, decimal> GetDefaultMovingAverageWeights() => new Dictionary<string, decimal>
        {
            {"SMA(10)", 0.05m}, {"EMA(10)", 0.1m}, {"SMA(20)", 0.05m}, {"EMA(20)", 0.1m},
            {"SMA(30)", 0.05m}, {"EMA(30)", 0.1m}, {"SMA(50)", 0.05m}, {"EMA(50)", 0.1m},
            {"SMA(100)", 0.1m}, {"EMA(100)", 0.1m}, {"SMA(200)", 0.05m}, {"EMA(200)", 0.05m},
            {"HMA(9)", 0.1m}
        };
    }
}
