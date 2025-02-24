using Binance.Net.Objects.Models.Spot;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using TechnicalAnalysis.Domain.Services.TechnicalAnalysis;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class TechnicalAnalysisService
    {
        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            IEnumerable<decimal> closePrices = data.Select(k => k.ClosePrice).ToList();

            OscillatorsService oscillatorsService = new OscillatorsService();
            MovingAveragesService movingAveragesService = new MovingAveragesService();

            var osciliators = oscillatorsService.PerformOscillators(data);
            var MAs = movingAveragesService.PerformMovingAverages(closePrices);

            var oscillatorIndicator = (double)oscillatorsService.CalculateOscillatorIndicatorValue(osciliators);
            var maIndicator = (double)movingAveragesService.CalculateMovingAverageIndicator(MAs);
            var generalIndicator = (oscillatorIndicator + maIndicator) / 2;

            TechnicalAnalysisResult technicalAnalysisResult = new TechnicalAnalysisResult(osciliators, MAs, oscillatorIndicator, maIndicator, generalIndicator);

            return technicalAnalysisResult;
        }
    }
}
