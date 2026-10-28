using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services
{
    public class MachineLearningService
    {
        private readonly AppDbContext _dbContext;
        private readonly MLContext _mlContext;
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
            _modelPath = configuration?["MachineLearning:ModelPath"] ?? "global_model.zip";
            _mlContext = new MLContext(seed: 42); // Фіксований seed для стабільності
        }

        public async Task<StrategyCandidatePreview> GenerateCandidatePreviewAsync(
            int candidatesPerStrategy = 20,
            int searchCombinationsPerStrategy = 10000)
        {
            if (!File.Exists(_modelPath))
                throw new InvalidOperationException("Модель еще не обучена. Сначала вызовите TrainModel.");
            if (candidatesPerStrategy <= 0 || candidatesPerStrategy > 100)
                throw new ArgumentOutOfRangeException(nameof(candidatesPerStrategy),
                    "Candidate count must be between 1 and 100.");
            if (searchCombinationsPerStrategy < candidatesPerStrategy)
                throw new ArgumentOutOfRangeException(nameof(searchCombinationsPerStrategy));

            var trainedModel = _mlContext.Model.Load(_modelPath, out _);
            var predictionEngine =
                _mlContext.Model.CreatePredictionEngine<OptimizationInput, OptimizationPrediction>(trainedModel);
            var combinations = await _dbContext.OptimizationRuns
                .Select(run => new { run.Asset, run.Timeframe })
                .Distinct()
                .OrderBy(run => run.Asset)
                .ThenBy(run => run.Timeframe)
                .ToListAsync();

            if (combinations.Count == 0)
                throw new InvalidOperationException("В базе данных нет результатов оптимизации.");

            var preview = new StrategyCandidatePreview
            {
                CandidatesPerStrategy = candidatesPerStrategy,
                SearchCombinationsPerStrategy = searchCombinationsPerStrategy
            };
            var random = new Random(42);

            foreach (var combination in combinations)
            {
                var bestCandidates = new List<PredictedStrategyCandidate>(candidatesPerStrategy + 1);
                for (int index = 0; index < searchCombinationsPerStrategy; index++)
                {
                    var parameters = GenerateRandomInput(random, combination.Timeframe, combination.Asset);
                    var prediction = predictionEngine.Predict(parameters);
                    bestCandidates.Add(new PredictedStrategyCandidate
                    {
                        Asset = combination.Asset,
                        Timeframe = combination.Timeframe,
                        PredictedScore = prediction.PredictedRiskAdjustedScore,
                        Parameters = parameters
                    });

                    if (bestCandidates.Count > candidatesPerStrategy)
                    {
                        int worstIndex = 0;
                        for (int candidateIndex = 1; candidateIndex < bestCandidates.Count; candidateIndex++)
                        {
                            if (bestCandidates[candidateIndex].PredictedScore <
                                bestCandidates[worstIndex].PredictedScore)
                                worstIndex = candidateIndex;
                        }

                        bestCandidates.RemoveAt(worstIndex);
                    }
                }

                preview.Candidates.AddRange(bestCandidates.OrderByDescending(candidate => candidate.PredictedScore));
            }

            return preview;
        }

        internal ITransformer TrainWalkForwardModel(IReadOnlyList<OptimizationInput> trainingRows)
        {
            if (trainingRows == null || trainingRows.Count < 100)
                throw new ArgumentException("At least 100 walk-forward training rows are required.",
                    nameof(trainingRows));

            IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingRows);
            var pipeline = _mlContext.Transforms
                .Categorical.OneHotEncoding("TimeframeEncoded", nameof(OptimizationInput.Timeframe))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(
                    "AssetEncoded",
                    nameof(OptimizationInput.Asset)))
                .Append(_mlContext.Transforms.Concatenate("Features", FeatureColumnNames))
                .Append(_mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));

            return pipeline.Fit(dataView);
        }

        internal PredictedStrategyCandidate PredictBestWalkForwardCandidate(
            ITransformer model,
            string asset,
            string timeframe,
            int searchCandidateCount,
            string seedScope)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var predictionEngine = _mlContext.Model
                .CreatePredictionEngine<OptimizationInput, OptimizationPrediction>(model);
            PredictedStrategyCandidate best = null;
            foreach (var parameters in StrategyCandidateGenerator.Generate(
                         asset,
                         timeframe,
                         searchCandidateCount,
                         seedScope))
            {
                float score = predictionEngine.Predict(parameters).PredictedRiskAdjustedScore;
                if (best == null || score > best.PredictedScore)
                {
                    best = new PredictedStrategyCandidate
                    {
                        Asset = asset,
                        Timeframe = timeframe,
                        PredictedScore = score,
                        Parameters = parameters
                    };
                }
            }

            return best ?? throw new InvalidOperationException("Walk-forward candidate search returned no candidates.");
        }

        // 2. ВИКОРИСТАННЯ МОДЕЛІ ДЛЯ ПОШУКУ НАЙКРАЩИХ ПАРАМЕТРІВ

        public async Task<string> TrainModelAsync()
        {
            Console.WriteLine("Выгрузка всех успешных прогонов из БД...");

            var runs = await _dbContext.OptimizationRuns.ToListAsync();

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
            sb.AppendLine("В обучение включены прогоны с нулевым и малым числом сделок.");
            sb.AppendLine(
                $"Label = сильный штраф при < {MinimumTrainingTrades} сделок; иначе Profit - {DrawdownPenaltyWeight:0.##} * MaxDrawdown - штраф ниже {PreferredTrainingTrades} сделок.");
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
            if (run.TotalTrades == 0)
                return -200f;
            if (run.TotalTrades < MinimumTrainingTrades)
                return (float)(-100m - 5m * (MinimumTrainingTrades - run.TotalTrades));

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