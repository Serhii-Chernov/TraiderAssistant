using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    internal class BullBearPowerOscillator : IOscillator
    {
        public string Name { get; set; } = "BullBearPower";
        public decimal Calculate(IEnumerable<BinanceSpotKline> data)
        {
            int period = 13; // Используем стандартный период EMA

            var closePrices = data.Select(x => x.ClosePrice).ToList();
            var highs = data.Select(x => x.HighPrice).ToList();
            var lows = data.Select(x => x.LowPrice).ToList();


            // Рассчитываем EMA за заданный период
            decimal ema = CalculateEMA(closePrices, period);

            // Bull Power = Максимальная цена - EMA
            decimal bullPower = highs.Last() - ema;

            // Bear Power = Минимальная цена - EMA
            decimal bearPower = lows.Last() - ema;

            // Итоговое значение как среднее между Bull и Bear Power
            return (bullPower + bearPower) / 2;
        }

        private decimal CalculateEMA(IEnumerable<decimal> data, int period)
        {
            if (data.Count() < period)
                throw new InvalidOperationException("Недостаточно данных для расчёта EMA.");

            decimal multiplier = 2m / (period + 1);
            decimal ema = data.Take(period).Average(); // Начальное значение — SMA

            foreach (var price in data.Skip(period))
            {
                ema = (price - ema) * multiplier + ema;
            }

            return ema;
        }
    }
}
