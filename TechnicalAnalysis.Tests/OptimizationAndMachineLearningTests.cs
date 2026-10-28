using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Shared;
using TechnicalAnalysis.WebApi.Controllers;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Tests;

public class OptimizationAndMachineLearningTests
{
    [Fact]
    public void OptimizationCombinationKey_UsesAssetTimeframeAndWeights()
    {
        const string weights = "{\"BuyThreshold\":30}";

        var baseline = BacktestService.GetOptimizationCombinationKey("BTCUSDT", "OneHour", weights);

        BacktestService.GetOptimizationCombinationKey("BTCUSDT", "OneHour", weights)
            .Should().Be(baseline);
        BacktestService.GetOptimizationCombinationKey("ETHUSDT", "OneHour", weights)
            .Should().NotBe(baseline);
        BacktestService.GetOptimizationCombinationKey("BTCUSDT", "FourHour", weights)
            .Should().NotBe(baseline);
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1, 199)]
    [InlineData(125, 75)]
    [InlineData(200, 0)]
    [InlineData(250, 0)]
    public void RemainingOptimizationCombinations_UsesTargetAsTotalCount(
        int existingCount,
        int expectedRemaining)
    {
        BacktestService.CalculateRemainingOptimizationCombinations(existingCount)
            .Should().Be(expectedRemaining);
    }

    [Fact]
    public void FeatureColumns_IncludeEncodedAsset()
    {
        MachineLearningService.FeatureColumnNames.Should().Contain("AssetEncoded");
    }

    [Fact]
    public void CreateTrainingInputs_ExcludesCorruptJsonAndReportsIt()
    {
        var runs = new[]
        {
            CreateRun(1, CreateValidWeightsJson()),
            CreateRun(2, "{not-valid-json")
        };

        var inputs = MachineLearningService.CreateTrainingInputs(runs, out var parsingErrors);

        inputs.Should().ContainSingle();
        inputs[0].Asset.Should().Be("BTCUSDT");
        parsingErrors.Should().ContainSingle().Which.Should().Contain("OptimizationRun 2");
    }

    [Fact]
    public async Task TrainModel_CountsOnlySuccessfullyParsedRuns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        for (var id = 1; id <= 99; id++)
        {
            dbContext.OptimizationRuns.Add(CreateRun(id, CreateValidWeightsJson()));
        }

        dbContext.OptimizationRuns.Add(CreateRun(100, "{not-valid-json"));
        await dbContext.SaveChangesAsync();

        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new MachineLearningService(dbContext, new StrategyProviderService(serviceProvider));

        var result = await service.TrainModelAsync();

        result.Should().Contain("(99)");
        result.Should().Contain("Поврежденных записей: 1");
    }

    [Fact]
    public void NormalizePairs_RemovesDuplicatesAndPreservesFirstSeenOrder()
    {
        var result = BacktestController.NormalizePairs(new[]
        {
            CurrencyPair.BTCUSDT,
            CurrencyPair.ETHUSDT,
            CurrencyPair.BTCUSDT
        });

        result.Should().Equal(CurrencyPair.BTCUSDT, CurrencyPair.ETHUSDT);
    }

    private static OptimizationRun CreateRun(int id, string weightsJson) => new()
    {
        Id = id,
        Asset = "BTCUSDT",
        Timeframe = "OneHour",
        IndicatorWeightsJson = weightsJson,
        TotalTrades = 1,
        TotalProfitPercent = 5m
    };

    private static string CreateValidWeightsJson() => JsonConvert.SerializeObject(new WeightsJsonSchema
    {
        OscillatorGroupWeight = 0.5m,
        MaGroupWeight = 0.5m,
        Oscillators = new Dictionary<string, decimal>(),
        MAs = new Dictionary<string, decimal>(),
        BuyThreshold = 30,
        SellThreshold = -30,
        StopLoss = 0.05m,
        TakeProfit = 0.1m
    });
}
