using Binance.Net.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System;
using System.Threading.Tasks;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Shared;
using Xunit;

namespace TechnicalAnalysis.Tests
{
    public class ApiAndSystemTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ApiAndSystemTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        // Перевірка ендпоінтів та обробки некоректних даних
        [Theory]
        [InlineData("/Analysis/Calculate?endTime=2024-01-01T00:00:00Z&currencyPair=BTCUSDT&klineInterval=OneDay", true)]
        [InlineData("/Analysis/Calculate?endTime=2024-01-01T00:00:00Z&currencyPair=INVALID_COIN&klineInterval=OneDay", false)]
        public async Task CalculateEndpoint_ShouldHandleValidAndInvalidAssets(string url, bool isValid)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            if (isValid)
            {
                response.IsSuccessStatusCode.Should().BeTrue(
                    $"Очікувався статус 200 OK, але отримали {response.StatusCode}. Деталі: {content}");

                content.Should().Contain("generalIndicator");
            }
            else
            {
                response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound,
                    "Шлях не знайдено (404). Сервер не бачить контролер.");

                response.IsSuccessStatusCode.Should().BeFalse("Запит з INVALID_COIN мав завершитися помилкою.");
            }
        }

        [Fact]
        public async Task RunAITestAsync_ShouldBuildEquityCurveAndCalculateMetrics()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var backtestService = scope.ServiceProvider.GetRequiredService<BacktestService>();

            var pair = CurrencyPair.BTCUSDT;
            var interval = KlineInterval.OneDay;
            var startTime = DateTime.UtcNow.AddMonths(-1);
            var endTime = DateTime.UtcNow;
            decimal initialDeposit = 1000m;

            // Act
            var result = await backtestService.RunAITestAsync(pair, interval, startTime, endTime, initialDeposit);

            // Assert
            result.Should().NotBeNull();
            result.FinalBalance.Should().BeGreaterThan(0);
            result.Trades.Should().NotBeNull();

            if (result.TotalTrades > 0)
            {
                result.ProfitFactor.Should().BeGreaterThanOrEqualTo(0);
                (result.WinningTrades + result.LosingTrades).Should().Be(result.TotalTrades);
            }
        }
    }
}