using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using TraiderAssistant.Infrastructure.Services;

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
                //MessageBox.Show($"TechAnalysisViewModel. indicatorValue :{value}");
                OnPropertyChanged();
                //UpdateIndicator();
            }
        }

        private string resultText;
        public string ResultText
        {
            get => resultText;
            private set
            {
                resultText = value;
                OnPropertyChanged();
            }
        }
        TechnicalAnalysisService techAnalysisService;

        public TechAnalysisViewModel()
        {
            techAnalysisService = new TechnicalAnalysisService();
            IndicatorValue = 0; // Нейтрально
        }

        private void UpdateIndicator()
        {
            // Обновляем текст и угол стрелки
            if (IndicatorValue < -50)
                ResultText = "Активно продавать";
            else if (IndicatorValue < 0)
                ResultText = "Продавать";
            else if (IndicatorValue == 0)
                ResultText = "Нейтрально";
            else if (IndicatorValue > 50)
                ResultText = "Активно покупать";
            else
                ResultText = "Покупать";
        }

        public double PerformTechnicalAnalysis(IEnumerable<double> closePrices)
        {
            IndicatorValue = techAnalysisService.CalculateRSI(closePrices);
            return IndicatorValue;
            //IndicatorValue = -10;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

