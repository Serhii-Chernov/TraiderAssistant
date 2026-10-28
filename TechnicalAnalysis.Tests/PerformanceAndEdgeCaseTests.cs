using FluentAssertions;
using Moq;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.Services;
using Xunit;

namespace TechnicalAnalysis.Tests
{
    public class PerformanceAndEdgeCaseTests
    {
        // імітація відсутності інтернет-з'єднання / таймауту БД
        [Fact]
        public void GetStrategy_WhenDatabaseOrNetworkFails_ShouldThrowOrHandleGracefully()
        {
            // Arrange
            var mockStrategyProvider = new Mock<StrategyProviderService>(new Mock<IServiceProvider>().Object);

            mockStrategyProvider
                .Setup(s => s.GetStrategy(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new TimeoutException("Connection to database or API timed out."));

            var service = new TechnicalAnalysisService(mockStrategyProvider.Object);

            // Act
            var fakeKlines = System.Linq.Enumerable.Range(1, 50)
                .Select(i => new Binance.Net.Objects.Models.Spot.BinanceSpotKline { ClosePrice = i * 10m })
                .ToList();

            Action act = () => service.PerformTechnicalAnalysis(fakeKlines, "BTCUSDT", "OneDay");

            // Assert
            // Перевіряємо, що система не "ковтає" критичні помилки мовчки, а коректно прокидає їх вище для обробки в Middleware
            act.Should().Throw<TimeoutException>()
               .WithMessage("Connection to database or API timed out.");
        }
    }
}