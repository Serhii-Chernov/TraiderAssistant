﻿using GalaSoft.MvvmLight.CommandWpf;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using TraiderAssistant.Infrastructure.Services;

namespace TraiderAssistant.UI.ViewModels
{
    //public enum ChartViewType
    //{
    //    Line,
    //    Area,
    //    Candle
    //}
    public class CurrencyChartViewModel : INotifyPropertyChanged
    {
        private readonly BinanceService _binanceService;
        public SeriesCollection Series { get; set; }
        public ObservableCollection<string> Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }//убрать в конкретный VM
        private readonly TechAnalysisViewModel _techAnalysisViewModel;
        public CurrencyPair CurrencyPair { get; set; }

        public ICommand LoadDayDataCommand { get; }
        public ICommand LoadWeekDataCommand { get; }
        public ICommand LoadMonthDataCommand { get; }
        public ICommand LoadYearDataCommand { get; }
        //public ICommand LoadTechnicalAnalysisCommand { get; }

        //private double _technicalAnalysisIndicator;
        //public double TechAnalysisIndicator 
        //{
        //    get { return _technicalAnalysisIndicator; }
        //    set
        //    {
        //        _technicalAnalysisIndicator = value;
        //        OnPropertyChanged(nameof(TechAnalysisIndicator));
        //    }
        //}
        private TechnicalAnalysisResult _technicalAnalysisResult;
        public TechnicalAnalysisResult TechnicalAnalysisResult
        {
            get { return _technicalAnalysisResult; }
            set
            {
                _technicalAnalysisResult = value;
                OnPropertyChanged(nameof(TechnicalAnalysisResult));
            }
        }

        public List<string> ChartTypes { get; }

        private string _selectedChartType;
        public string SelectedChartType
        {
            get => _selectedChartType;
            set
            {
                _selectedChartType = value;
                OnPropertyChanged(nameof(SelectedChartType));
                LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow).ConfigureAwait(false); // Перезагрузка данных при изменении стиля графика
            }
        }

        public CurrencyChartViewModel(TechAnalysisViewModel techAnalysisViewModel, CurrencyPair currencyPair)
        {
            CurrencyPair = currencyPair;
            _binanceService = new BinanceService(CurrencyPair);

            _techAnalysisViewModel = techAnalysisViewModel;
            LoadDayDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
            LoadWeekDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow));
            LoadMonthDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow));
            LoadYearDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow));

            ChartTypes = new List<string> { "Line", "Area", "Candle" };
            SelectedChartType = "Line"; // Установите начальный стиль графика

            YFormatter = value => value.ToString("N5"); // Округление до целых чисел

            InitializeAsync().ConfigureAwait(false);
        }

        private async Task InitializeAsync()
        {
            await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            await InitializeTechnicalAnalysis(DateTime.UtcNow.AddMinutes(-250), DateTime.UtcNow);
        }

        private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
        {
            //Period = endTime.Subtract(startTime).Days;
            var data = await _binanceService.GetChartDataAsync(startTime, endTime);
            var prices = data.Select(k => k.ClosePrice).ToArray();
            var dates = data.Select(k => k.CloseTime.ToString("dd/MM/yyyy")).ToArray();
            

            Series = new SeriesCollection();

            switch (SelectedChartType)
            {
                case "Line":
                    Series.Add(new LineSeries
                    {
                        Title = "Notcoin",
                        Values = new ChartValues<decimal>(prices),
                        PointGeometry = null, // Убираем круги
                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
                    });
                    break;
                case "Area":
                    Series.Add(new LineSeries
                    {
                        Title = "Notcoin",
                        Values = new ChartValues<decimal>(prices),
                        Fill = CreateGradientBrush(prices),
                        StrokeThickness = 1,
                        PointGeometry = null, // Убираем круги
                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
                    });
                    break;
                case "Candle":
                    var candleData = data.Select(k => new OhlcPoint((double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice)).ToArray();
                    Series.Add(new CandleSeries
                    {
                        Title = "Notcoin",
                        Values = new ChartValues<OhlcPoint>(candleData)
                    });
                    break;
            }

            Labels = new ObservableCollection<string>(dates);
            OnPropertyChanged(nameof(Series));
            OnPropertyChanged(nameof(Labels));
        }

        private async Task InitializeTechnicalAnalysis(DateTime startTime, DateTime endTime)
        {
            var data = await _binanceService.GetChartDataForIndicatorsAsync(endTime);
            TechnicalAnalysisResult = _techAnalysisViewModel.PerformTechnicalAnalysis(data);
            //technicalAnalysisResult.ind;
        }

        private Brush CreateGradientBrush(decimal[] prices)
        {
            var gradientStops = new GradientStopCollection();
            var maxPrice = prices.Max();
            var minPrice = prices.Min();

            for (int i = 0; i < prices.Length; i++)
            {
                var offset = (double)(prices[i] - minPrice) / (double)(maxPrice - minPrice);
                var alpha = (byte)(255 * offset);
                var color = Color.FromArgb(alpha, 224, 178, 25); // Используем синий цвет с изменением альфа-канала
                gradientStops.Add(new GradientStop(color, (double)i / (prices.Length - 1)));
            }

            return new LinearGradientBrush(gradientStops, 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
