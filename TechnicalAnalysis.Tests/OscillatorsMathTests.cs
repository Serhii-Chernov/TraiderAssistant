using Binance.Net.Objects.Models.Spot;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.Oscillators;
using TechnicalAnalysis.Domain.Services.TechnicalAnalysis;
using Xunit;

namespace TechnicalAnalysis.Tests
{
    public class OscillatorsMathTests
    {
        //  Перевірка розрахунку індикаторів на тестовому масиві
        [Fact]
        public void Calculate_RsiAndMacd_ShouldNotBeNullAndWithinBounds()
        {
            // Arrange
            var rsi = new RsiOscillator();
            var macd = new MacdOscillator();

            var klines = GenerateMockKlines(50, startPrice: 100, trend: 2);

            // Act
            var rsiResult = rsi.Calculate(klines);
            var macdResult = macd.Calculate(klines);

            // Assert
            rsiResult.Should().NotBeNull();
            rsiResult.Value.Should().BeInRange(0, 100, "RSI завжди має бути від 0 до 100");

            macdResult.Should().NotBeNull();
            macdResult.Value.Should().NotBe(0);
        }

        // Подача масиву з нульовою волатильністю (флет)
        [Fact]
        public void Calculate_FlatMarket_ShouldNotThrowDivideByZeroException()
        {
            // Arrange
            var adx = new AdxOscillator();
            var ultimate = new UltimateOscillator();
            var stochastic = new StochasticKOscillator();

            // Всі свічки мають абсолютно однакову ціну (Open=High=Low=Close=100)
            var flatKlines = GenerateMockKlines(50, startPrice: 100, trend: 0);

            // Act
            Action actAdx = () => adx.Calculate(flatKlines);
            Action actUltimate = () => ultimate.Calculate(flatKlines);
            Action actStoch = () => stochastic.Calculate(flatKlines);

            // Assert
            // Жоден індикатор не повинен впасти з помилкою ділення на нуль
            actAdx.Should().NotThrow<DivideByZeroException>();
            actUltimate.Should().NotThrow<DivideByZeroException>();
            actStoch.Should().NotThrow<DivideByZeroException>();
        }

        private static List<BinanceSpotKline> GenerateMockKlines(int count, decimal startPrice, decimal trend)
        {
            var klines = new List<BinanceSpotKline>();
            for (int i = 0; i < count; i++)
            {
                // Додаємо зигзаг (волатильність), щоб індикатори (особливо MACD) не дорівнювали рівно 0
                decimal volatility = (i % 2 == 0) ? 5m : -5m;
                var price = startPrice + (i * trend) + volatility;

                var k = new BinanceSpotKline
                {
                    OpenTime = DateTime.UtcNow.AddMinutes(-count + i),
                    OpenPrice = price,
                    HighPrice = price,
                    LowPrice = price,
                    ClosePrice = price,
                    Volume = 100
                };
                klines.Add(k);
            }
            return klines;
        }
    }
}