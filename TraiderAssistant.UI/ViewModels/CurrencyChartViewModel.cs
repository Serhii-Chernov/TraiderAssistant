using Binance.Net.Enums;
using GalaSoft.MvvmLight.CommandWpf;
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
using TechnicalAnalysis.Shared;

namespace TraiderAssistant.UI.ViewModels
{
    public class CurrencyChartViewModel : INotifyPropertyChanged
    {
        public SeriesCollection Series { get; set; }
        public ObservableCollection<string> Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }
        TechnicalAnalysisClient _technicalAnalysisClient;
        public CurrencyPair CurrencyPair { get; set; }
        
        public ICommand SetChartEndTimeCommand { get; }
        public ICommand SetTechnicalAnalysisKlineIntervalCommand { get; }

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

        private DateTime _selectedChartEndTime;
        public DateTime SelectedChartEndTime
        {
            get => _selectedChartEndTime;
            set
            {
                _selectedChartEndTime = value;
                OnPropertyChanged(nameof(SelectedChartEndTime));
                LoadDataAsync(SelectedChartEndTime, DateTime.UtcNow).ConfigureAwait(false); // Вызов LoadDataAsync при изменении периода графика
            }
        }

        private KlineInterval _technicalAnalysisKlineInterval;
        public KlineInterval TechnicalAnalysisKlineInterval
        {
            get => _technicalAnalysisKlineInterval;
            set
            {
                _technicalAnalysisKlineInterval = value;
                OnPropertyChanged(nameof(TechnicalAnalysisKlineInterval));
                InitializeTechnicalAnalysis(DateTime.UtcNow, TechnicalAnalysisKlineInterval).ConfigureAwait(false); // Вызов InitializeTechnicalAnalysis при изменении интервала
            }
        }

        public CurrencyChartViewModel(TechnicalAnalysisClient technicalAnalysisClient, CurrencyPair currencyPair)
        {
            CurrencyPair = currencyPair;

            _technicalAnalysisClient = technicalAnalysisClient;
            SetChartEndTimeCommand = new RelayCommand<int>(days => SelectedChartEndTime = DateTime.UtcNow.AddDays(-days));
            SetTechnicalAnalysisKlineIntervalCommand = new RelayCommand<KlineInterval>(interval => TechnicalAnalysisKlineInterval = interval);

            ChartTypes = new List<string> { "Line", "Area", "Candle" };
            SelectedChartType = "Line"; // Установите начальный стиль графика
            SelectedChartEndTime = DateTime.UtcNow.AddDays(-1); // Установите начальный период графика

            YFormatter = value => value.ToString("N5"); // Округление до целых чисел

            InitializeAsync().ConfigureAwait(false);
        }

        private async Task InitializeAsync()
        {
            //await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            await InitializeTechnicalAnalysis(DateTime.UtcNow);
        }

        private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
        {
            var data = await _technicalAnalysisClient.GetChartDataAsync(CurrencyPair, startTime, endTime);
            var prices = data.Select(k => k.ClosePrice).ToArray();
            var dates = data.Select(k => k.CloseTime.ToString("dd/MM/yyyy")).ToArray();

            StringBuilder title = new StringBuilder();
            var roundedValue = Math.Round(prices.Last(), 5);
            title.Append($"{CurrencyPair.ToString()}  {roundedValue.ToString()}");
            Series = new SeriesCollection();

            switch (SelectedChartType)
            {
                case "Line":

                    Series.Add(new LineSeries
                    {
                        Title = title.ToString(),
                        Values = new ChartValues<decimal>(prices),
                        PointGeometry = null, // Убираем круги
                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
                    });
                    break;
                case "Area":
                    Series.Add(new LineSeries
                    {

                        Title = title.ToString(),
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
                        Title = title.ToString(),
                        Values = new ChartValues<OhlcPoint>(candleData)
                    });
                    break;
            }

            Labels = new ObservableCollection<string>(dates);
            OnPropertyChanged(nameof(Series));
            OnPropertyChanged(nameof(Labels));
        }

        private async Task InitializeTechnicalAnalysis(DateTime endTime, KlineInterval klineInterval = KlineInterval.OneMinute)
        {
            TechnicalAnalysisResult = await _technicalAnalysisClient.GetTechnicalAnalysisAsync(CurrencyPair, endTime, klineInterval);
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
