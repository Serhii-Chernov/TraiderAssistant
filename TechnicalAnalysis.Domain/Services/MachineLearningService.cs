using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain.MLModels;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services
{
    public class MachineLearningService
    {
        private readonly AppDbContext _dbContext;
        private readonly MLContext _mlContext;
        private readonly StrategyProviderService _strategyProvider;
        private readonly string _modelPath;

        internal const int MinimumTrainingTrades = 10;
        internal const int PreferredTrainingTrades = 30;
        internal const decimal DrawdownPenaltyWeight = 1.5m;
        internal const decimal MissingTradePenalty = 0.25m;

        internal static readonly string[] FeatureColumnNames =
        {
            "TimeframeEncoded", "AssetEncoded",
            nameof(OptimizationInput.RsiWeight), nameof(OptimizationInput.StochasticKWeight),
            nameof(OptimizationInput.CciWeight),
            nameof(OptimizationInput.AdxWeight), nameof(OptimizationInput.AoWeight),
            nameof(OptimizationInput.MomentumWeight),
            nameof(OptimizationInput.MacdWeight), nameof(OptimizationInput.UltimateWeight),
            nameof(OptimizationInput.WilliamsRWeight),
            nameof(OptimizationInput.BullBearWeight), nameof(OptimizationInput.StochasticRsiWeight),
            nameof(OptimizationInput.Sma10Weight), nameof(OptimizationInput.Ema10Weight),
            nameof(OptimizationInput.Sma20Weight),
            nameof(OptimizationInput.Ema20Weight), nameof(OptimizationInput.Sma30Weight),
            nameof(OptimizationInput.Ema30Weight),
            nameof(OptimizationInput.Sma50Weight), nameof(OptimizationInput.Ema50Weight),
            nameof(OptimizationInput.Sma100Weight),
            nameof(OptimizationInput.Ema100Weight), nameof(OptimizationInput.Sma200Weight),
            nameof(OptimizationInput.Ema200Weight),
            nameof(OptimizationInput.Hma9Weight),
            nameof(OptimizationInput.BuyThreshold), nameof(OptimizationInput.SellThreshold),
            nameof(OptimizationInput.StopLoss), nameof(OptimizationInput.TakeProfit),
            nameof(OptimizationInput.OscillatorGroupWeight), nameof(OptimizationInput.MaGroupWeight)
        };

        public MachineLearningService(
            AppDbContext dbContext,
            StrategyProviderService strategyProvider,
            IConfiguration configuration = null)
        {
            _dbContext = dbContext;
            _strategyProvider = strategyProvider;
            _modelPath = configuration?["MachineLearning:ModelPath"] ?? "global_model.zip";
            _mlContext = new MLContext(seed: 42); // Фіксований seed для стабільності
        }

        // 2. ВИКОРИСТАННЯ МОДЕЛІ ДЛЯ ПОШУКУ НАЙКРАЩИХ ПАРАМЕТРІВ
        public async Task<string> FindBestStrategiesForAllTimeframes()
        {
            if (!File.Exists(_modelPath)) return "Модель еще не обучена! Сначала вызовите TrainModel.";

            // Загружаем обученную модель
            DataViewSchema modelSchema;
            ITransformer trainedModel = _mlContext.Model.Load(_modelPath, out modelSchema);
            var predEngine =
                _mlContext.Model.CreatePredictionEngine<OptimizationInput, OptimizationPrediction>(trainedModel);

            var rnd = new Random();
            int combinationsToTry = 10000;

            var strategyCombinations = await _dbContext.OptimizationRuns
                .Where(r => r.TotalTrades >= MinimumTrainingTrades)
                .Select(r => new { r.Asset, r.Timeframe })
                .Distinct()
                .ToListAsync();

            if (strategyCombinations.Count == 0)
                return "В базе данных нет успешных связок монета+таймфрейм для анализа.";

            var summaryResults = new Dictionary<string, (float Profit, OptimizationInput Strategy)>();

            Console.WriteLine(
                $"Запуск глобального ИИ-анализа. {combinationsToTry} комбинаций на каждую связку Монета+Таймфрейм...");

            foreach (var combination in strategyCombinations)
            {
                OptimizationInput bestStrategy = null;
                float bestPredictedScore = float.MinValue;

                for (int i = 0; i < combinationsToTry; i++)
                {
                    var input = GenerateRandomInput(rnd, combination.Timeframe, combination.Asset);
                    var prediction = predEngine.Predict(input);

                    if (prediction.PredictedRiskAdjustedScore > bestPredictedScore)
                    {
                        bestPredictedScore = prediction.PredictedRiskAdjustedScore;
                        bestStrategy = input;
                    }
                }

                string key = $"{combination.Asset} | {combination.Timeframe}";
                summaryResults[key] = (bestPredictedScore, bestStrategy);
            }

            // === АВТОМАТИЧЕСКОЕ ОБНОВЛЕНИЕ БОЕВОЙ БАЗЫ ДАННЫХ ===
            Console.WriteLine("Сохранение точных результатов ИИ в базу данных...");

            // 1. Удаляем старые стратегии
            var oldStrategies = await _dbContext.ActiveStrategies.ToListAsync();
            _dbContext.ActiveStrategies.RemoveRange(oldStrategies);

            // 2. Формируем новые стратегии с абсолютной машинной точностью
            // СТАЛО:
            var newActiveStrategies = summaryResults.Select(r => r.Value).Select(entry =>
            {
                var strategy = entry.Strategy;
                var weightsSchema = new WeightsJsonSchema
                {
                    OscillatorGroupWeight = (decimal)strategy.OscillatorGroupWeight,
                    MaGroupWeight = (decimal)strategy.MaGroupWeight,

                    Oscillators = new Dictionary<string, decimal>
                    {
                        { "RSI", (decimal)strategy.RsiWeight }, { "StochasticK", (decimal)strategy.StochasticKWeight },
                        { "CCI", (decimal)strategy.CciWeight }, { "ADX", (decimal)strategy.AdxWeight },
                        { "AO", (decimal)strategy.AoWeight }, { "Momentum", (decimal)strategy.MomentumWeight },
                        { "MACD", (decimal)strategy.MacdWeight },
                        { "UltimateOscillator", (decimal)strategy.UltimateWeight },
                        { "Williams%R", (decimal)strategy.WilliamsRWeight },
                        { "BullBearPower", (decimal)strategy.BullBearWeight },
                        { "StochasticRSI", (decimal)strategy.StochasticRsiWeight }
                    },

                    MAs = new Dictionary<string, decimal>
                    {
                        { "SMA(10)", (decimal)strategy.Sma10Weight }, { "EMA(10)", (decimal)strategy.Ema10Weight },
                        { "SMA(20)", (decimal)strategy.Sma20Weight }, { "EMA(20)", (decimal)strategy.Ema20Weight },
                        { "SMA(30)", (decimal)strategy.Sma30Weight }, { "EMA(30)", (decimal)strategy.Ema30Weight },
                        { "SMA(50)", (decimal)strategy.Sma50Weight }, { "EMA(50)", (decimal)strategy.Ema50Weight },
                        { "SMA(100)", (decimal)strategy.Sma100Weight }, { "EMA(100)", (decimal)strategy.Ema100Weight },
                        { "SMA(200)", (decimal)strategy.Sma200Weight }, { "EMA(200)", (decimal)strategy.Ema200Weight },
                        { "HMA(9)", (decimal)strategy.Hma9Weight }
                    },
                    BuyThreshold = (int)strategy.BuyThreshold,
                    SellThreshold = (int)strategy.SellThreshold,
                    StopLoss = (decimal)strategy.StopLoss,
                    TakeProfit = (decimal)strategy.TakeProfit
                };

                return new TraiderAssistant.Infrastructure.Entities.ActiveStrategy
                {
                    Asset = entry.Strategy.Asset,
                    Timeframe = entry.Strategy.Timeframe,
                    BuyThreshold = (int)strategy.BuyThreshold,
                    SellThreshold = (int)strategy.SellThreshold,
                    StopLoss = (decimal)strategy.StopLoss,
                    TakeProfit = (decimal)strategy.TakeProfit,
                    IndicatorWeightsJson = JsonConvert.SerializeObject(weightsSchema)
                };
            }).ToList();

            await _dbContext.ActiveStrategies.AddRangeAsync(newActiveStrategies);
            await _dbContext.SaveChangesAsync();
            await _strategyProvider.LoadStrategiesFromDatabaseAsync();
            Console.WriteLine("Боевые стратегии успешно обновлены!");

            // Сортируем результаты от лучшего к худшему для красивого вывода в Swagger
            var sortedResults = summaryResults.OrderByDescending(x => x.Value.Profit).ToList();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== ГЛОБАЛЬНЫЙ ОТЧЕТ ИИ ПО МОНЕТАМ И ТАЙМФРЕЙМАМ ===\n");
            sb.AppendLine("[БОЕВЫЕ НАСТРОЙКИ УСПЕШНО ЗАГРУЖЕНЫ В БАЗУ ДАННЫХ]\n");

            int rank = 1;
            foreach (var result in sortedResults)
            {
                var key = result.Key;
                var profit = result.Value.Profit;
                var strategy = result.Value.Strategy;

                sb.AppendLine($"[{rank}] {key}");
                sb.AppendLine($"Максимальный ожидаемый risk-adjusted score: {profit:0.##}");
                sb.AppendLine($"--- Настройки Риск-менеджмента ---");
                sb.AppendLine(
                    $"Buy > {strategy.BuyThreshold} | Sell < {strategy.SellThreshold} | StopLoss: {strategy.StopLoss:P} | TakeProfit: {strategy.TakeProfit:P}");

                var oscDict = new Dictionary<string, float>
                {
                    { "RSI", strategy.RsiWeight }, { "Stoch", strategy.StochasticKWeight },
                    { "CCI", strategy.CciWeight },
                    { "ADX", strategy.AdxWeight }, { "AO", strategy.AoWeight }, { "Momentum", strategy.MomentumWeight },
                    { "MACD", strategy.MacdWeight }, { "Ultimate", strategy.UltimateWeight },
                    { "Will%R", strategy.WilliamsRWeight },
                    { "BullBear", strategy.BullBearWeight }, { "StochRSI", strategy.StochasticRsiWeight }
                };

                var maDict = new Dictionary<string, float>
                {
                    { "SMA10", strategy.Sma10Weight }, { "EMA10", strategy.Ema10Weight },
                    { "SMA20", strategy.Sma20Weight },
                    { "EMA20", strategy.Ema20Weight }, { "SMA30", strategy.Sma30Weight },
                    { "EMA30", strategy.Ema30Weight },
                    { "SMA50", strategy.Sma50Weight }, { "EMA50", strategy.Ema50Weight },
                    { "SMA100", strategy.Sma100Weight },
                    { "EMA100", strategy.Ema100Weight }, { "SMA200", strategy.Sma200Weight },
                    { "EMA200", strategy.Ema200Weight },
                    { "HMA9", strategy.Hma9Weight }
                };

                var topOsc = oscDict.OrderByDescending(x => x.Value).Take(3).ToList();
                var topMa = maDict.OrderByDescending(x => x.Value).Take(3).ToList();

                sb.AppendLine($"--- Главные драйверы стратегии (ТОП-3) ---");
                sb.AppendLine(
                    $"Осцилляторы: {topOsc[0].Key} ({topOsc[0].Value:P1}) | {topOsc[1].Key} ({topOsc[1].Value:P1}) | {topOsc[2].Key} ({topOsc[2].Value:P1})");
                sb.AppendLine(
                    $"Трендовые:   {topMa[0].Key} ({topMa[0].Value:P1}) | {topMa[1].Key} ({topMa[1].Value:P1}) | {topMa[2].Key} ({topMa[2].Value:P1})");
                sb.AppendLine("--------------------------------------------------\n");

                rank++;
            }

            var bestOverall = sortedResults[0];
            var worstOverall = sortedResults[sortedResults.Count - 1];

            sb.AppendLine("=== ИТОГОВЫЙ ВЫВОД ИИ ===");
            sb.AppendLine(
                $"🏆 ЛУЧШИЙ КАНДИДАТ: '{bestOverall.Key}' имеет наивысший risk-adjusted score ({bestOverall.Value.Profit:0.##}).");
            sb.AppendLine($"🛑 ХУДШИЙ КАНДИДАТ: '{worstOverall.Key}' имеет score {worstOverall.Value.Profit:0.##}.");
            sb.AppendLine(
                "Кандидаты должны пройти независимый forward-test; score не является прогнозом процента прибыли.");

            return sb.ToString();
        }

        public async Task<string> TrainModelAsync()
        {
            Console.WriteLine("Выгрузка всех успешных прогонов из БД...");

            var runs = await _dbContext.OptimizationRuns
                .Where(r => r.TotalTrades >= MinimumTrainingTrades)
                .ToListAsync();

            var mlInputData = CreateTrainingInputs(runs, out var parsingErrors);
            foreach (var parsingError in parsingErrors)
            {
                Console.Error.WriteLine(parsingError);
            }

            if (mlInputData.Count < 100)
            {
                return $"Недостаточно корректных данных для обучения ({mlInputData.Count}). " +
                       $"Поврежденных записей: {parsingErrors.Count}. Нужно хотя бы 100 корректных прогонов.";
            }

            Console.WriteLine($"Данные подготовлены: {mlInputData.Count} строк. Строим Pipeline...");

            IDataView dataView = _mlContext.Data.LoadFromEnumerable(mlInputData);
            var trainTestSplit = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

            // 1. ОТДЕЛЯЕМ КОНВЕЙЕР ПОДГОТОВКИ ДАННЫХ
            var dataProcessPipeline = _mlContext.Transforms
                .Categorical.OneHotEncoding("TimeframeEncoded", nameof(OptimizationInput.Timeframe))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding("AssetEncoded",
                    nameof(OptimizationInput.Asset)))
                .Append(_mlContext.Transforms.Concatenate("Features", FeatureColumnNames));

            // 2. ОБУЧАЕМ ОБРАБОТЧИК И ТРАНСФОРМИРУЕМ ДАННЫЕ
            var dataPrepModel = dataProcessPipeline.Fit(trainTestSplit.TrainSet);
            var transformedTrainData = dataPrepModel.Transform(trainTestSplit.TrainSet);
            var transformedTestData = dataPrepModel.Transform(trainTestSplit.TestSet);

            // 3. СОЗДАЕМ И ОБУЧАЕМ ПРЕДИКТОР НА ТРАНСФОРМИРОВАННЫХ ДАННЫХ
            var trainer =
                _mlContext.Regression.Trainers.FastTree(labelColumnName: "Label", featureColumnName: "Features");
            var predictor = trainer.Fit(transformedTrainData);

            // 4. ОЦЕНИВАЕМ ТОЧНОСТЬ
            var predictions = predictor.Transform(transformedTestData);
            var metrics =
                _mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            // 5. СОХРАНЯЕМ ПОЛНУЮ МОДЕЛЬ ДЛЯ ДРУГИХ МЕТОДОВ
            var fullModel = dataPrepModel.Append(predictor);
            var modelDirectory = Path.GetDirectoryName(Path.GetFullPath(_modelPath));
            Directory.CreateDirectory(modelDirectory!);
            _mlContext.Model.Save(fullModel, dataView.Schema, _modelPath);

            // === ВЗЛАМЫВАЕМ МОЗГ ИИ (РАСЧЕТ PFI) ===
            Console.WriteLine("Оценка важности признаков (PFI) на тестовой выборке...");

            // Скармливаем алгоритму только предиктор и уже трансформированные данные
            var pfi = _mlContext.Regression.PermutationFeatureImportance(predictor, transformedTestData,
                permutationCount: 3, labelColumnName: "Label");

            // Извлекаем имена слотов безопасно (без конфликтов с async/await и лямбдами)
            string[] slotNamesArray = GetSlotNames(transformedTestData.Schema["Features"]);

            // Сопоставляем результаты PFI с именами слотов по их индексу
            var pfiRanking = pfi
                .Select((metrics, index) => new
                {
                    Feature = index < slotNamesArray.Length ? slotNamesArray[index] : $"Feature_{index}",
                    RSquaredDrop = metrics.RSquared.Mean
                })
                .OrderByDescending(x => x.RSquaredDrop)
                .ToList();

            // Формируем отчет
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Глобальная risk-adjusted модель обучена на {mlInputData.Count} записях!");
            sb.AppendLine($"Поврежденных записей пропущено: {parsingErrors.Count}.");
            sb.AppendLine($"Минимум сделок в обучающей записи: {MinimumTrainingTrades}.");
            sb.AppendLine(
                $"Label = Profit - {DrawdownPenaltyWeight:0.##} * MaxDrawdown - штраф за число сделок ниже {PreferredTrainingTrades}.");
            sb.AppendLine($"R-Squared: {metrics.RSquared:0.####} | RMSE score: {metrics.RootMeanSquaredError:0.##}\n");

            sb.AppendLine("=== ТОП-10 САМЫХ ВАЖНЫХ ПАРАМЕТРОВ (PFI) ===");
            sb.AppendLine("Чем выше падение метрики, тем важнее этот параметр для алгоритма:");

            int rank = 1;
            foreach (var item in pfiRanking.Take(10))
            {
                sb.AppendLine($"[{rank}] {item.Feature.PadRight(25)} -> Влияние: {item.RSquaredDrop:P2}");
                rank++;
            }

            return sb.ToString();

            // Локальная синхронная функция для обхода ограничений C# (ref struct нельзя в async)
            static string[] GetSlotNames(Microsoft.ML.DataViewSchema.Column column)
            {
                Microsoft.ML.Data.VBuffer<ReadOnlyMemory<char>> names = default;
                column.Annotations.GetValue("SlotNames", ref names);
                var span = names.GetValues(); // Получаем тот самый проблемный ReadOnlySpan
                var arr = new string[span.Length];
                for (int i = 0; i < span.Length; i++)
                {
                    arr[i] = span[i].ToString(); // Перегоняем в обычный массив строк
                }

                return arr;
            }
        }

        internal static List<OptimizationInput> CreateTrainingInputs(
            IEnumerable<OptimizationRun> runs,
            out List<string> parsingErrors)
        {
            var inputs = new List<OptimizationInput>();
            parsingErrors = new List<string>();

            foreach (var run in runs)
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<WeightsJsonSchema>(run.IndicatorWeightsJson)
                                 ?? throw new JsonSerializationException("JSON contains null.");

                    if (config.Oscillators is null || config.MAs is null)
                    {
                        throw new JsonSerializationException("Oscillators or MAs section is missing.");
                    }

                    inputs.Add(new OptimizationInput
                    {
                        Asset = run.Asset,
                        Timeframe = run.Timeframe,

                        RsiWeight = (float)config.Oscillators.GetValueOrDefault("RSI", 0),
                        StochasticKWeight = (float)config.Oscillators.GetValueOrDefault("StochasticK", 0),
                        CciWeight = (float)config.Oscillators.GetValueOrDefault("CCI", 0),
                        AdxWeight = (float)config.Oscillators.GetValueOrDefault("ADX", 0),
                        AoWeight = (float)config.Oscillators.GetValueOrDefault("AO", 0),
                        MomentumWeight = (float)config.Oscillators.GetValueOrDefault("Momentum", 0),
                        MacdWeight = (float)config.Oscillators.GetValueOrDefault("MACD", 0),
                        UltimateWeight = (float)config.Oscillators.GetValueOrDefault("UltimateOscillator", 0),
                        WilliamsRWeight = (float)config.Oscillators.GetValueOrDefault("Williams%R", 0),
                        BullBearWeight = (float)config.Oscillators.GetValueOrDefault("BullBearPower", 0),
                        StochasticRsiWeight = (float)config.Oscillators.GetValueOrDefault("StochasticRSI", 0),

                        Sma10Weight = (float)config.MAs.GetValueOrDefault("SMA(10)", 0),
                        Ema10Weight = (float)config.MAs.GetValueOrDefault("EMA(10)", 0),
                        Sma20Weight = (float)config.MAs.GetValueOrDefault("SMA(20)", 0),
                        Ema20Weight = (float)config.MAs.GetValueOrDefault("EMA(20)", 0),
                        Sma30Weight = (float)config.MAs.GetValueOrDefault("SMA(30)", 0),
                        Ema30Weight = (float)config.MAs.GetValueOrDefault("EMA(30)", 0),
                        Sma50Weight = (float)config.MAs.GetValueOrDefault("SMA(50)", 0),
                        Ema50Weight = (float)config.MAs.GetValueOrDefault("EMA(50)", 0),
                        Sma100Weight = (float)config.MAs.GetValueOrDefault("SMA(100)", 0),
                        Ema100Weight = (float)config.MAs.GetValueOrDefault("EMA(100)", 0),
                        Sma200Weight = (float)config.MAs.GetValueOrDefault("SMA(200)", 0),
                        Ema200Weight = (float)config.MAs.GetValueOrDefault("EMA(200)", 0),
                        Hma9Weight = (float)config.MAs.GetValueOrDefault("HMA(9)", 0),

                        BuyThreshold = config.BuyThreshold,
                        SellThreshold = config.SellThreshold,
                        StopLoss = (float)config.StopLoss,
                        TakeProfit = (float)config.TakeProfit,
                        OscillatorGroupWeight = (float)config.OscillatorGroupWeight,
                        MaGroupWeight = (float)config.MaGroupWeight,
                        RiskAdjustedScore = CalculateRiskAdjustedScore(run)
                    });
                }
                catch (Exception ex) when (ex is JsonException || ex is NullReferenceException)
                {
                    parsingErrors.Add(
                        $"OptimizationRun {run.Id} ({run.Asset}/{run.Timeframe}) has invalid weights JSON: {ex.Message}");
                }
            }

            return inputs;
        }

        internal static float CalculateRiskAdjustedScore(OptimizationRun run)
        {
            int missingTrades = Math.Max(0, PreferredTrainingTrades - run.TotalTrades);
            decimal score = run.TotalProfitPercent
                            - DrawdownPenaltyWeight * run.MaxDrawdown
                            - MissingTradePenalty * missingTrades;
            return (float)score;
        }

        private OptimizationInput GenerateRandomInput(Random rnd, string timeframe, string asset)
        {
            float[] oscW = new float[11];
            float oscSum = 0;
            for (int i = 0; i < 11; i++)
            {
                oscW[i] = (float)rnd.NextDouble();
                oscSum += oscW[i];
            }

            for (int i = 0; i < 11; i++) oscW[i] /= oscSum;

            float[] maW = new float[13];
            float maSum = 0;
            for (int i = 0; i < 13; i++)
            {
                maW[i] = (float)rnd.NextDouble();
                maSum += maW[i];
            }

            for (int i = 0; i < 13; i++) maW[i] /= maSum;

            float oscGroup = (float)rnd.NextDouble();
            float maGroup = 1.0f - oscGroup;

            return new OptimizationInput
            {
                Asset = asset,
                Timeframe = timeframe, // Вказуємо ШІ, для якого таймфрейму ці налаштування

                RsiWeight = oscW[0],
                StochasticKWeight = oscW[1],
                CciWeight = oscW[2],
                AdxWeight = oscW[3],
                AoWeight = oscW[4],
                MomentumWeight = oscW[5],
                MacdWeight = oscW[6],
                UltimateWeight = oscW[7],
                WilliamsRWeight = oscW[8],
                BullBearWeight = oscW[9],
                StochasticRsiWeight = oscW[10],

                Sma10Weight = maW[0],
                Ema10Weight = maW[1],
                Sma20Weight = maW[2],
                Ema20Weight = maW[3],
                Sma30Weight = maW[4],
                Ema30Weight = maW[5],
                Sma50Weight = maW[6],
                Ema50Weight = maW[7],
                Sma100Weight = maW[8],
                Ema100Weight = maW[9],
                Sma200Weight = maW[10],
                Ema200Weight = maW[11],
                Hma9Weight = maW[12],

                BuyThreshold = rnd.Next(10, 60),
                SellThreshold = rnd.Next(-60, -10),
                StopLoss = (float)(rnd.NextDouble() * 0.04 + 0.01),
                TakeProfit = (float)(rnd.NextDouble() * 0.13 + 0.02),

                OscillatorGroupWeight = oscGroup,
                MaGroupWeight = maGroup
            };
        }
    }
}