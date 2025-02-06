using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TraiderAssistant.Infrastructure.Services.TechnicalAnalysis.TradeAction;

namespace TraiderAssistant.Infrastructure.Services.TechnicalAnalysis
{
    public class TechnicalAnalysisResult
    {
        public double OscillatorIndicator { get; set; }
        public string OscillatorResultText { get; set; }

        public double MAIndicator { get; set; }
        public string MAResultText { get; set; }

        public double GeneralIndicator { get; set; }
        public string GeneralResultText { get; set; }

        public List<TechnicalAnalysisNameValueActionStruct> osciliators;
        public List<TechnicalAnalysisNameValueActionStruct> movingAverages;

        public TechnicalAnalysisResult(List<TechnicalAnalysisNameValueActionStruct> osciliators, List<TechnicalAnalysisNameValueActionStruct> movingAverages, double oscillatorIndicator, double maIndicator, double generalIndicator)
        {
            this.osciliators = osciliators;
            this.movingAverages = movingAverages;
            this.OscillatorIndicator = oscillatorIndicator;
            this.MAIndicator = maIndicator;
            this.GeneralIndicator = generalIndicator;
            OscillatorResultText = UpdateResultText(OscillatorIndicator);
            MAResultText = UpdateResultText(MAIndicator);
            GeneralResultText = UpdateResultText(GeneralIndicator);
        }
        private string UpdateResultText(double indicatorValue)
        {
            if (indicatorValue < -50)
                return TradeAction.TradeAction.ActiveSell;
            else if (indicatorValue < -15)
                return TradeAction.TradeAction.Sell;
            else if (indicatorValue > -15 && indicatorValue < 15)
                return TradeAction.TradeAction.Neutral;
            else if (indicatorValue > 50)
                return TradeAction.TradeAction.ActiveBuy;
            else
                return TradeAction.TradeAction.Buy;
        }
    }
}
