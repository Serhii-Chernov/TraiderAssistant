using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public interface IOscillator
    {
        public string Name { get; set; }
        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data);
        public string GetAction(decimal value, decimal? extraValue = null);
    }
}
