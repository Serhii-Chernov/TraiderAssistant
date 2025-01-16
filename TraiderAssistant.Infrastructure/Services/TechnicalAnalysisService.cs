using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Services
{
    public class TechnicalAnalysisService
    {
        public double CalculateRSI(IEnumerable<double> closePrices, int period = 14)
        {
            var gains = new List<double>();
            var losses = new List<double>();

            for (int i = 1; i < closePrices.Count(); i++)
            {
                var change = closePrices.ElementAt(i) - closePrices.ElementAt(i - 1);
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(Math.Abs(change));
            }

            var avgGain = gains.TakeLast(period).Average();
            var avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0) return 100;
            var rs = avgGain / avgLoss;

            return 100 - (100 / (1 + rs));
        }
    }
}
