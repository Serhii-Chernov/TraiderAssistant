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

        TechnicalAnalysisService techAnalysisService;

        public TechAnalysisViewModel()
        {
            techAnalysisService = new TechnicalAnalysisService();
            IndicatorValue = 0; // Нейтрально
        }

        public TechnicalAnalysisResult PerformTechnicalAnalysis(IEnumerable<BinanceSpotKline> data)
        {
            return techAnalysisService.PerformTechnicalAnalysis(data);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

