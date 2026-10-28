using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Domain.Services;
using TechnicalAnalysis.Domain.Services.TechnicalAnalysis;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class TechnicalAnalysisService
    {
        private readonly OscillatorsService _oscillatorsService = new OscillatorsService();
        private readonly MovingAveragesService _movingAveragesService = new MovingAveragesService();
        private readonly StrategyProviderService _strategyProvider;

        public TechnicalAnalysisService(StrategyProviderService strategyProvider)
        {
            _strategyProvider = strategyProvider;
        }

        public TechnicalAnalysisResult PerformTechnicalAnalysis(
            IEnumerable<BinanceSpotKline> data,
            Dictionary<string, decimal> oscWeights,
            Dictionary<string, decimal> maWeights)
        {
            IEnumerable<decimal> closePrices = data.Select(k => k.ClosePrice).ToList();

            var osciliators = _oscillatorsService.PerformOscillators(data);
            var MAs = _movingAveragesService.PerformMovingAverages(closePrices);

            var oscillatorIndicator = (double)_oscillatorsService.CalculateOscillatorIndicatorValue(osciliators, oscWeights);
            var maIndicator = (double)_movingAveragesService.CalculateMovingAverageIndicator(MAs, maWeights);
            var generalIndicator = (oscillatorIndicator + maIndicator) / 2;

            return new TechnicalAnalysisResult(osciliators, MAs, oscillatorIndicator, maIndicator, generalIndicator);
        }

        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data, string asset, string timeframe)
        {
            IEnumerable<decimal> closePrices = data.Select(k => k.ClosePrice).ToList();

            var osciliators = _oscillatorsService.PerformOscillators(data);
            var MAs = _movingAveragesService.PerformMovingAverages(closePrices);

            double oscillatorIndicator;
            double maIndicator;
            double generalIndicator;

            var strategy = _strategyProvider.GetStrategy(asset, timeframe);

            if (strategy != null && strategy.Weights != null)
            {
                oscillatorIndicator = (double)_oscillatorsService.CalculateOscillatorIndicatorValue(osciliators, strategy.Weights.Oscillators);
                maIndicator = (double)_movingAveragesService.CalculateMovingAverageIndicator(MAs, strategy.Weights.MAs);

                double oscGroupWeight = strategy.Weights.OscillatorGroupWeight > 0 ? (double)strategy.Weights.OscillatorGroupWeight : 0.5;
                double maGroupWeight = strategy.Weights.MaGroupWeight > 0 ? (double)strategy.Weights.MaGroupWeight : 0.5;

                generalIndicator = (oscillatorIndicator * oscGroupWeight) + (maIndicator * maGroupWeight);
            }
            else
            {
                oscillatorIndicator = (double)_oscillatorsService.CalculateOscillatorIndicatorValue(osciliators);
                maIndicator = (double)_movingAveragesService.CalculateMovingAverageIndicator(MAs);
                generalIndicator = (oscillatorIndicator + maIndicator) / 2;
            }

            return new TechnicalAnalysisResult(osciliators, MAs, oscillatorIndicator, maIndicator, generalIndicator);
        }

        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            return PerformTechnicalAnalysis(data, "UNKNOWN", "UNKNOWN");
        }
    }
}