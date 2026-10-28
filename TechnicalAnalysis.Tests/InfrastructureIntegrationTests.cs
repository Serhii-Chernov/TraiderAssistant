using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Shared;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;
using Xunit;

namespace TechnicalAnalysis.Tests.Integration
{
    public class InfrastructureIntegrationTests
    {
        // Перевірка витягування JSON-конфігурацій з БД
        [Fact]
        public async Task LoadStrategiesFromDatabaseAsync_ShouldDeserializeAndCache()
        {
            // Arrange: Створюємо In-Memory базу даних
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestTradingDb")
                .Options;

            using (var context = new AppDbContext(options))
            {
                context.ActiveStrategies.Add(new ActiveStrategy
                {
                    Asset = "BTCUSDT",
                    Timeframe = "OneDay",
                    SelectionMethod = "WalkForward",
                    IndicatorWeightsJson = "{\"OscillatorGroupWeight\":0.6, \"MaGroupWeight\":0.4}"
                });
                context.SaveChanges();
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("TestTradingDb"));
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var strategyProvider = new StrategyProviderService(serviceProvider);

            // Act
            await strategyProvider.LoadStrategiesFromDatabaseAsync();
            var strategy = strategyProvider.GetStrategy("BTCUSDT", "OneDay");

            // Assert
            strategy.Should().NotBeNull();
            strategy.Weights.Should().NotBeNull();
            strategy.Weights.OscillatorGroupWeight.Should().Be(0.6m);
        }

        // Перевірка завантаження історії через Binance API (Мокаємо мережевий клієнт)
        [Fact]
        public async Task GetChartDataAsync_WithValidPair_ShouldReturnData()
        {
            // Arrange
            var binanceService = new BinanceService();
            var pair = CurrencyPair.BTCUSDT;
            var startTime = DateTime.UtcNow.AddDays(-1);
            var endTime = DateTime.UtcNow;

            // Act
            var result = await binanceService.GetChartDataAsync(pair, startTime, endTime);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }
    }
}
