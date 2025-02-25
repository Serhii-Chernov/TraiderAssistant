﻿using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain
{
    public class RSIOscillator : IOscillator
    {
        public string Name { get; set; } = "RSI";

        public TechnicalAnalysisNameValueActionStruct Calculate(IEnumerable<BinanceSpotKline> data)
        {
            
            int period = 14;
            var closePrices = data.Select(k => k.ClosePrice).ToList();
            if (closePrices.Count < period + 1)
                throw new ArgumentException("Not enough data to calculate.");

            decimal sumGain = 0;
            decimal sumLoss = 0;

            // We calculate the initial amounts of gains and losses
            for (int i = 1; i <= period; i++)
            {
                decimal delta = closePrices[i] - closePrices[i - 1];
                if (delta > 0)
                    sumGain += delta;
                else
                    sumLoss -= delta;
            }

            decimal avgGain = sumGain / period;
            decimal avgLoss = sumLoss / period;

            // We calculate subsequent average gains and losses
            for (int i = period + 1; i < closePrices.Count; i++)
            {
                decimal delta = closePrices[i] - closePrices[i - 1];

                avgGain = (avgGain * (period - 1) + (delta > 0 ? delta : 0)) / period;
                avgLoss = (avgLoss * (period - 1) + (delta < 0 ? -delta : 0)) / period;
            }
            TechnicalAnalysisNameValueActionStruct technicalAnalysisStruct = new TechnicalAnalysisNameValueActionStruct();
            technicalAnalysisStruct.Name = Name;
            if(avgGain == 0 || avgLoss == 0)
            {
                if (avgLoss == 0)
                    technicalAnalysisStruct.Value = 100;
                if (avgGain == 0)
                    technicalAnalysisStruct.Value = 0;
            }
            else
            {
                decimal rs = avgGain / avgLoss;
                technicalAnalysisStruct.Value = 100 - (100 / (1 + rs));
            }

            technicalAnalysisStruct.Action = GetAction(technicalAnalysisStruct.Value);
            return technicalAnalysisStruct;
        }

        public string GetAction(decimal value, decimal? extraValue = null)
        {
            return value > 70 ? TradeAction.Sell : (value < 30 ? TradeAction.Buy : TradeAction.Neutral);
        }
    }
}
