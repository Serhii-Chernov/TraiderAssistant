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
using TraiderAssistant.Infrastructure.Services;

namespace TraiderAssistant.UI.ViewModels
{
    public enum ChartViewType
    {
        Line,
        Area,
        Candle
    }
    public class BaseVM : INotifyPropertyChanged
    {
        private readonly BinanceService _binanceService;
        public SeriesCollection Series { get; set; }
        public ObservableCollection<string> Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }//убрать в конкретный VM
        private readonly TechAnalysisViewModel _techAnalysisViewModel;
        private IEnumerable<decimal> Hights { get; set; }
        private IEnumerable<decimal> Lows { get; set; }
        public int Period { get; set; }

        public ICommand LoadDayDataCommand { get; }
        public ICommand LoadWeekDataCommand { get; }
        public ICommand LoadMonthDataCommand { get; }
        public ICommand LoadYearDataCommand { get; }
        //public ICommand LoadTechnicalAnalysisCommand { get; }

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

        //public List<string> ChartTypes { get; }

        private int _selectedChartViewType;
        public int SelectedChartViewType
        {
            get => _selectedChartViewType;
            set
            {
                _selectedChartViewType = value;
                OnPropertyChanged(nameof(SelectedChartViewType));
                LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow).ConfigureAwait(false); // Перезагрузка данных при изменении стиля графика
            }
        }

        public BaseVM(TechAnalysisViewModel techAnalysisViewModel, CurrencyPair currencyPair)
        {
            _binanceService = new BinanceService(currencyPair);

            _techAnalysisViewModel = techAnalysisViewModel;
            LoadDayDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
            LoadWeekDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow));
            LoadMonthDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow));
            LoadYearDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow));
            //LoadTechnicalAnalysisCommand = new RelayCommand(OpenTechnicalAnalysisWindow);

            //ChartTypes = new List<string> { "Line", "Area", "Candle" };
            SelectedChartViewType = 0; // начальный стиль графика
            Period=14; // начальный период для технического анализа

            YFormatter = value => value.ToString("N5"); // Округление до целых чисел

            InitializeAsync().ConfigureAwait(false);
            //LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow).ConfigureAwait(false); // Вызов метода при инициализации
        }

        private async Task InitializeAsync()
        {
            await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            InitializeTechnicalAnalysis();
        }

        private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
        {
            var data = await _binanceService.GetChartDataAsync(startTime, endTime);
            var prices = data.Select(k => k.ClosePrice).ToArray();
            var dates = data.Select(k => k.CloseTime.ToString("dd/MM/yyyy")).ToArray();
            Hights = data.Select(k => k.HighPrice).ToArray();
            Lows = data.Select(k => k.LowPrice).ToArray();

            Series = new SeriesCollection();

            switch (SelectedChartViewType)
            {
                case 0:
                    Series.Add(new LineSeries
                    {
                        Title = "Notcoin",
                        Values = new ChartValues<decimal>(prices),
                        PointGeometry = null, // Убираем круги
                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
                    });
                    break;
                case 1:
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
                case 2:
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
            OnPropertyChanged(nameof(Hights));
            OnPropertyChanged(nameof(Lows));
        }

        private void InitializeTechnicalAnalysis()
        {
            var closePrices = Series.FirstOrDefault()?.Values.Cast<decimal>().Select(v => (double)v) ?? Enumerable.Empty<double>();
            var indicatorValue = _techAnalysisViewModel.PerformTechnicalAnalysis(closePrices, Hights, Lows, Period);
            TechnicalAnalysisResult = new TechnicalAnalysisResult(indicatorValue);
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
