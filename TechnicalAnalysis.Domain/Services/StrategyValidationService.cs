using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Models;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services;

public class StrategyValidationService
{
    private readonly MachineLearningService _machineLearningService;
    private readonly BacktestService _backtestService;
    private readonly AppDbContext _dbContext;
    private readonly StrategyProviderService _strategyProvider;

    public StrategyValidationService(
        MachineLearningService machineLearningService,
        BacktestService backtestService,
        AppDbContext dbContext,
        StrategyProviderService strategyProvider)
    {
        _machineLearningService = machineLearningService;
        _backtestService = backtestService;
        _dbContext = dbContext;
        _strategyProvider = strategyProvider;
    }

    public async Task<StrategyValidationReport> BuildValidatedStrategiesAsync(
        DateTime startTime,
        DateTime endTime,
        decimal initialDeposit,
        int candidatesPerStrategy = 20,
        int searchCombinationsPerStrategy = 10000)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be greater than start time.", nameof(endTime));
        if (initialDeposit <= 0m)
            throw new ArgumentOutOfRangeException(nameof(initialDeposit));

        var preview = await _machineLearningService.GenerateCandidatePreviewAsync(
            candidatesPerStrategy,
            searchCombinationsPerStrategy);
        var report = new StrategyValidationReport
        {
            StartDate = startTime,
            EndDate = endTime,
            InitialDeposit = initialDeposit,
            CandidatesPerStrategy = candidatesPerStrategy
        };
        var acceptedStrategies = new List<ActiveStrategy>();

        foreach (var group in preview.Candidates.GroupBy(candidate => new { candidate.Asset, candidate.Timeframe }))
        {
            var candidates = group.OrderByDescending(candidate => candidate.PredictedScore).ToList();
            var evaluations = await _backtestService.ValidateCandidatesAsync(
                group.Key.Asset,
                group.Key.Timeframe,
                candidates,
                startTime,
                endTime,
                initialDeposit);
            var selected = evaluations
                .Where(evaluation => evaluation.IsAccepted)
                .OrderByDescending(evaluation => evaluation.ActualScore)
                .ThenByDescending(evaluation => evaluation.PredictedScore)
                .FirstOrDefault();

            report.Strategies.Add(new StrategyGroupValidationResult
            {
                Asset = group.Key.Asset,
                Timeframe = group.Key.Timeframe,
                CandidatesEvaluated = evaluations.Count,
                HasAcceptedStrategy = selected != null,
                SelectedStrategy = selected,
                Candidates = evaluations
            });

            if (selected != null)
                acceptedStrategies.Add(CreateActiveStrategy(group.Key.Asset, group.Key.Timeframe, selected.Parameters));
        }

        var oldStrategies = await _dbContext.ActiveStrategies.ToListAsync();
        _dbContext.ActiveStrategies.RemoveRange(oldStrategies);
        await _dbContext.ActiveStrategies.AddRangeAsync(acceptedStrategies);
        await _dbContext.SaveChangesAsync();
        await _strategyProvider.LoadStrategiesFromDatabaseAsync();

        report.ActiveStrategiesCreated = acceptedStrategies.Count;
        report.Strategies = report.Strategies
            .OrderByDescending(strategy => strategy.HasAcceptedStrategy)
            .ThenBy(strategy => strategy.Asset)
            .ThenBy(strategy => strategy.Timeframe)
            .ToList();
        return report;
    }

    internal static ActiveStrategy CreateActiveStrategy(
        string asset,
        string timeframe,
        OptimizationInput parameters)
    {
        var weights = new WeightsJsonSchema
        {
            OscillatorGroupWeight = (decimal)parameters.OscillatorGroupWeight,
            MaGroupWeight = (decimal)parameters.MaGroupWeight,
            Oscillators = new Dictionary<string, decimal>
            {
                { "RSI", (decimal)parameters.RsiWeight },
                { "StochasticK", (decimal)parameters.StochasticKWeight },
                { "CCI", (decimal)parameters.CciWeight },
                { "ADX", (decimal)parameters.AdxWeight },
                { "AO", (decimal)parameters.AoWeight },
                { "Momentum", (decimal)parameters.MomentumWeight },
                { "MACD", (decimal)parameters.MacdWeight },
                { "UltimateOscillator", (decimal)parameters.UltimateWeight },
                { "Williams%R", (decimal)parameters.WilliamsRWeight },
                { "BullBearPower", (decimal)parameters.BullBearWeight },
                { "StochasticRSI", (decimal)parameters.StochasticRsiWeight }
            },
            MAs = new Dictionary<string, decimal>
            {
                { "SMA(10)", (decimal)parameters.Sma10Weight },
                { "EMA(10)", (decimal)parameters.Ema10Weight },
                { "SMA(20)", (decimal)parameters.Sma20Weight },
                { "EMA(20)", (decimal)parameters.Ema20Weight },
                { "SMA(30)", (decimal)parameters.Sma30Weight },
                { "EMA(30)", (decimal)parameters.Ema30Weight },
                { "SMA(50)", (decimal)parameters.Sma50Weight },
                { "EMA(50)", (decimal)parameters.Ema50Weight },
                { "SMA(100)", (decimal)parameters.Sma100Weight },
                { "EMA(100)", (decimal)parameters.Ema100Weight },
                { "SMA(200)", (decimal)parameters.Sma200Weight },
                { "EMA(200)", (decimal)parameters.Ema200Weight },
                { "HMA(9)", (decimal)parameters.Hma9Weight }
            },
            BuyThreshold = (int)parameters.BuyThreshold,
            SellThreshold = (int)parameters.SellThreshold,
            StopLoss = (decimal)parameters.StopLoss,
            TakeProfit = (decimal)parameters.TakeProfit
        };

        return new ActiveStrategy
        {
            Asset = asset,
            Timeframe = timeframe,
            IndicatorWeightsJson = JsonConvert.SerializeObject(weights),
            BuyThreshold = weights.BuyThreshold,
            SellThreshold = weights.SellThreshold,
            StopLoss = weights.StopLoss,
            TakeProfit = weights.TakeProfit
        };
    }
}
