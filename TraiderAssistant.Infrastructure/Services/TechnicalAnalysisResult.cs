using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Services
{
    public class TechnicalAnalysisResult
    {
        public TechnicalAnalysisResult(double indicator)
        {
            Indicator = indicator;
        }

        public double Indicator { get; set; }
    }
}
