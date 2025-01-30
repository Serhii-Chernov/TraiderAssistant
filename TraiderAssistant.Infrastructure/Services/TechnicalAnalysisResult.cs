using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Services
{
    public class TechnicalAnalysisResult
    {
        public double Indicator { get; set; }
        public string ResultText { get; set; }
        private Dictionary<string, decimal> osciliators;
        public TechnicalAnalysisResult(double indicator, Dictionary<string, decimal> osciliators)
        {
            Indicator = indicator;
            this.osciliators = osciliators;
            UpdateResultText();
        }

        
        private void UpdateResultText()
        {
            if (Indicator != 0)
            {
                // Обновляем текст и угол стрелки
                if (Indicator < -50)
                    ResultText = "Активно продавать";
                else if (Indicator < 0)
                    ResultText = "Продавать";
                else if (Indicator == 0)
                    ResultText = "Нейтрально";
                else if (Indicator > 50)
                    ResultText = "Активно покупать";
                else
                    ResultText = "Покупать";
            }
        }
    }

}
