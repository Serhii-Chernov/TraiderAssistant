using Binance.Net.Objects.Models.Spot;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using TraiderAssistant.Infrastructure.Services.TechnicalAnalysis;

namespace TraiderAssistant.UI.ViewModels
{
    public class TechAnalysisViewModel : INotifyPropertyChanged
    {
        private double indicatorValue;

        public double IndicatorValue
        {
            get => indicatorValue;
            set
            {
                indicatorValue = value;
                OnPropertyChanged();
                //UpdateIndicator();
            }
        }

        //private string resultText;
        //public string ResultText
        //{
        //    get => resultText;
        //    private set
        //    {
        //        resultText = value;
        //        OnPropertyChanged();
        //    }
        //}
        TechnicalAnalysisService techAnalysisService;

        public TechAnalysisViewModel()
        {
            techAnalysisService = new TechnicalAnalysisService();
            IndicatorValue = 0; // Нейтрально
        }

        //private void UpdateIndicator()
        //{
        //    // Обновляем текст и угол стрелки
        //    if (IndicatorValue < -50)
        //        ResultText = "Активно продавать";
        //    else if (IndicatorValue < 0)
        //        ResultText = "Продавать";
        //    else if (IndicatorValue == 0)
        //        ResultText = "Нейтрально";
        //    else if (IndicatorValue > 50)
        //        ResultText = "Активно покупать";
        //    else
        //        ResultText = "Покупать";
        //}

        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            //var result = techAnalysisService.GetTechnicalAnalysis(closePrices, hights, lows, period);
            return techAnalysisService.PerformTechnicalAnalysis(data);
            //IndicatorValue = techAnalysisService.GetTechnicalAnalysis(closePrices, hights, lows, period);
            //return IndicatorValue;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

