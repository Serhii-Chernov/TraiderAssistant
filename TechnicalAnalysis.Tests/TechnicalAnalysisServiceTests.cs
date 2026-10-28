using Binance.Net.Objects.Models.Spot;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.MLModels;
using TechnicalAnalysis.Domain.Services;
using Xunit;

namespace TechnicalAnalysis.Tests.Integration
{
    public class TechnicalAnalysisServiceTests
    {
        [Fact]
        public void PerformTechnicalAnalysis_WithAIStrategy_ShouldApplyDatabaseWeights()
        {
            // Arrange
            var asset = "BTCUSDT";
            var timeframe = "OneDay";

            // Створюємо фейкові свічки (мінімум 14 для RSI)
            var fakeKlines = new List<BinanceSpotKline>();
            for (int i = 0; i < 50; i++)
            {
                fakeKlines.Add(new BinanceSpotKline { ClosePrice = 50000m + i * 100 });
            }

            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockStrategyProvider = new Mock<StrategyProviderService>(mockServiceProvider.Object);

            // Налаштовуємо фейкову відповідь від кешу (використовуємо ActiveStrategyModel)
            var fakeAiStrategy = new ActiveStrategyModel
            {
                BuyThreshold = 30,
                SellThreshold = -30,
                Weights = new WeightsJsonSchema
                {
                    OscillatorGroupWeight = 0.7m,
                    MaGroupWeight = 0.3m,
                    Oscillators = new Dictionary<string, decimal> { { "RSI", 1.0m } },
                    MAs = new Dictionary<string, decimal> { { "SMA(10)", 1.0m } }
                }
            };

            mockStrategyProvider
                .Setup(s => s.GetStrategy(asset, timeframe))
                .Returns(fakeAiStrategy);

            var service = new TechnicalAnalysisService(mockStrategyProvider.Object);

            // Act
            var result = service.PerformTechnicalAnalysis(fakeKlines, asset, timeframe);

            // Assert
            result.Should().NotBeNull();
            result.Oscillators.Should().HaveCount(11);
            result.MovingAverages.Should().NotBeEmpty();

            // Перевіряємо, що зведений індикатор розрахувався
            result.GeneralIndicator.Should().BeInRange(-100, 100);

            // Перевіряємо, що сервіс справді звертався до нашого "фейкового" провайдера
            mockStrategyProvider.Verify(s => s.GetStrategy(asset, timeframe), Times.Once);
        }
    }
}
