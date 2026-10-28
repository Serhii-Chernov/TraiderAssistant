using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TechnicalAnalysis.Domain.Services.TechnicalAnalysis;
using TechnicalAnalysis.Shared;
using Xunit;

namespace TechnicalAnalysis.Tests.Unit
{
    public class MovingAveragesServiceTests
    {
        private readonly MovingAveragesService _movingAveragesService;

        public MovingAveragesServiceTests()
        {
            _movingAveragesService = new MovingAveragesService();
        }

        [Fact]
        public void PerformMovingAverages_WithValidData_ShouldReturnCalculatedMAs()
        {
            // Arrange
            // Генеруємо масив з 25 свічок (щоб вистачило для SMA 10 і 20)
            var prices = Enumerable.Range(1, 25).Select(x => (decimal)x).ToList();

            // Act (Дія)
            var result = _movingAveragesService.PerformMovingAverages(prices);

            // Assert (Перевірка)
            result.Should().NotBeNull();
            result.Should().Contain(x => x.Name == "SMA(10)");
            result.Should().Contain(x => x.Name == "EMA(10)");
            result.Should().Contain(x => x.Name == "SMA(20)");
            result.Should().NotContain(x => x.Name == "SMA(50)"); // Даних для 50 свічок недостатньо
        }

        [Fact]
        public void CalculateMovingAverageIndicator_WithCustomWeights_ShouldReturnCorrectScore()
        {
            // Arrange
            var maStructs = new List<TechnicalAnalysisNameValueActionStruct>
            {
                new TechnicalAnalysisNameValueActionStruct { Name = "SMA(10)", Action = TradeAction.Buy },  // +1
                new TechnicalAnalysisNameValueActionStruct { Name = "EMA(10)", Action = TradeAction.Sell }, // -1
                new TechnicalAnalysisNameValueActionStruct { Name = "HMA(9)", Action = TradeAction.Buy }    // +1
            };

            var weights = new Dictionary<string, decimal>
            {
                { "SMA(10)", 0.2m },
                { "EMA(10)", 0.3m },
                { "HMA(9)", 0.5m }
            };

            // Act
            // Формула: (1*0.2) + (-1*0.3) + (1*0.5) = 0.2 - 0.3 + 0.5 = 0.4
            // 0.4 / (0.2 + 0.3 + 0.5) * 100 = 0.4 / 1.0 * 100 = 40
            var result = _movingAveragesService.CalculateMovingAverageIndicator(maStructs, weights);

            // Assert
            result.Should().Be(40m);
        }

        /// <summary>
        /// Verifies that HMA uses a rolling difference series before final WMA smoothing.
        /// </summary>
        [Fact]
        public void PerformMovingAverages_WithLinearPrices_ShouldCalculateHullMovingAverage()
        {
            var prices = Enumerable.Range(1, 20).Select(value => (decimal)value).ToList();

            var result = _movingAveragesService.PerformMovingAverages(prices);

            result.Single(item => item.Name == "HMA(9)").Value.Should().Be(20m);
        }
    }
}
