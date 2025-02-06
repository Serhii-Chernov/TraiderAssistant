using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Oscillators
{
    public interface IOscillator
    {
        public string Name { get; set; }
        //internal decimal Calculate(IEnumerable<decimal> prices, int period);
        public decimal Calculate(IEnumerable<BinanceSpotKline> data);
    }
}
