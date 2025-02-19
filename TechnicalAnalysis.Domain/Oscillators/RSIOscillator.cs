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

            // Вычисляем начальные суммы приростов и убытков
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

            // Вычисляем последующие средние приросты и убытки
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


    //public class RSIOscillator : IOscillator
    //{
    //    public string Name { get; set; } = "RSI";

    //    public decimal Calculate(IEnumerable<BinanceSpotKline> data)
    //    {
    //        int period = 14;
    //        var closePrices = data.Select(k => k.ClosePrice).ToList();
    //        if (closePrices.Count < period)
    //            throw new ArgumentException("Not enough data to calculate.");

    //        decimal sumGain = 0;
    //        decimal sumLoss = 0;

    //        // Calculate initial average gain and loss
    //        for (int i = 1; i < period; i++)
    //        {
    //            decimal delta = closePrices[i] - closePrices[i - 1];
    //            if (delta > 0)
    //                sumGain += delta;
    //            else
    //                sumLoss -= delta;
    //        }

    //        decimal avgGain = sumGain / (period - 1);
    //        decimal avgLoss = sumLoss / (period - 1);

    //        // Calculate subsequent average gain and loss
    //        for (int i = period; i < closePrices.Count; i++)
    //        {
    //            decimal delta = closePrices[i] - closePrices[i - 1];
    //            if (delta > 0)
    //            {
    //                avgGain = (avgGain * (period - 1) + delta) / period;
    //                avgLoss = (avgLoss * (period - 1)) / period;
    //            }
    //            else
    //            {
    //                avgGain = (avgGain * (period - 1)) / period;
    //                avgLoss = (avgLoss * (period - 1) - delta) / period;
    //            }
    //        }

    //        if (avgLoss == 0)
    //            return 100;
    //        if (avgGain == 0)
    //            return 0;

    //        decimal rs = avgGain / avgLoss;
    //        return 100 - 100 / (1 + rs);
    //    }
    //}

}
