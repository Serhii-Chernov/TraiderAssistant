//using GalaSoft.MvvmLight.Command;
//using LiveCharts;
//using LiveCharts.Defaults;
//using LiveCharts.Wpf;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Input;
//using System.Windows.Media;
//using TraiderAssistant.Infrastructure.Services;
//using TraiderAssistant.UI.Views;

//namespace TraiderAssistant.UI.ViewModels
//{
//    public class BitcoinChartViewModel : INotifyPropertyChanged
//    {
//        private readonly BinanceService _binanceService;
//        private readonly TechAnalysisViewModel _techAnalysisViewModel;

//        public SeriesCollection Series { get; set; }
//        public ObservableCollection<string> Labels { get; set; }
//        public Func<double, string> YFormatter { get; set; }

//        public ICommand LoadDayDataCommand { get; }
//        public ICommand LoadWeekDataCommand { get; }
//        public ICommand LoadMonthDataCommand { get; }
//        public ICommand LoadYearDataCommand { get; }
//        public ICommand AnalyzeCommand { get; }
//        //public ICommand LoadTechnicalAnalysisCommand { get; }

//        public List<string> ChartTypes { get; }

//        private string _selectedChartType;
//        public string SelectedChartType
//        {
//            get => _selectedChartType;
//            set
//            {
//                _selectedChartType = value;
//                OnPropertyChanged(nameof(SelectedChartType));
//                LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow).ConfigureAwait(false); // Перезагрузка данных при изменении стиля графика
//            }
//        }

//        public BitcoinChartViewModel(TechAnalysisViewModel techAnalysisViewModel)
//        {
//            _binanceService = new BinanceService(CurrencyPair.BTCUSDT);
//            _techAnalysisViewModel = techAnalysisViewModel;

//            LoadDayDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
//            LoadWeekDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow));
//            LoadMonthDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow));
//            LoadYearDataCommand = new RelayCommand(async () => await LoadDataAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow));
//            //AnalyzeCommand = new RelayCommand(Analyze);
//            //LoadTechnicalAnalysisCommand = new RelayCommand(OpenTechnicalAnalysisWindow);

//            ChartTypes = new List<string> { "Line", "Area", "Candle" };
//            SelectedChartType = "Line"; // Установите начальный стиль графика

//            YFormatter = value => value.ToString("N0"); // Округление до целых чисел

//            //LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow).ConfigureAwait(false); // Вызов метода при инициализации
//            //Task.Run(() => LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow)).Wait();
//            //InitializeTechnicalAnalysis();

//            InitializeAsync().ConfigureAwait(false);
//        }

//        private async Task InitializeAsync()
//        {
//            await LoadDataAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
//            InitializeTechnicalAnalysis();
//        }

//        private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
//        {
//            var data = await _binanceService.GetChartDataAsync(startTime, endTime);
//            var prices = data.Select(k => k.ClosePrice).ToArray();
//            var dates = data.Select(k => k.CloseTime.ToString("MM/dd/yyyy")).ToArray();

//            Series = new SeriesCollection();

//            switch (SelectedChartType)
//            {
//                case "Line":
//                    Series.Add(new LineSeries
//                    {
//                        Title = "Bitcoin",
//                        Values = new ChartValues<decimal>(prices),
//                        PointGeometry = null, // Убираем круги
//                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
//                    });
//                    break;
//                case "Area":
//                    Series.Add(new LineSeries
//                    {
//                        Title = "Bitcoin",
//                        Values = new ChartValues<decimal>(prices),
//                        Fill = CreateGradientBrush(prices),
//                        StrokeThickness = 1,
//                        PointGeometry = null, // Убираем круги
//                        PointGeometrySize = 0 // Устанавливаем размер кругов в 0
//                    });
//                    break;
//                case "Candle":
//                    var candleData = data.Select(k => new OhlcPoint((double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice)).ToArray();
//                    Series.Add(new CandleSeries
//                    {
//                        Title = "Bitcoin",
//                        Values = new ChartValues<OhlcPoint>(candleData)
//                    });
//                    break;
//            }

//            Labels = new ObservableCollection<string>(dates);
//            OnPropertyChanged(nameof(Series));
//            OnPropertyChanged(nameof(Labels));
//        }

//        private void InitializeTechnicalAnalysis()
//        {
//            var closePrices = Series.FirstOrDefault()?.Values.Cast<decimal>().Select(v => (double)v) ?? Enumerable.Empty<double>();
//            //var indicatorValue = _techAnalysisViewModel.PerformTechnicalAnalysis(closePrices);
//            var indicatorValue = 30;
//            //TechnicalAnalysisResult = new TechnicalAnalysisResult(indicatorValue);
//        }

//        private Brush CreateGradientBrush(decimal[] prices)
//        {
//            var gradientStops = new GradientStopCollection();
//            var maxPrice = prices.Max();
//            var minPrice = prices.Min();

//            for (int i = 0; i < prices.Length; i++)
//            {
//                var offset = (double)(prices[i] - minPrice) / (double)(maxPrice - minPrice);
//                var alpha = (byte)(255 * offset);
//                var color = Color.FromArgb(alpha, 224, 178, 25);
//                gradientStops.Add(new GradientStop(color, (double)i / (prices.Length - 1)));
//            }

//            return new LinearGradientBrush(gradientStops, 0);
//        }


//        public event PropertyChangedEventHandler PropertyChanged;
//        protected virtual void OnPropertyChanged(string propertyName)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        private TechnicalAnalysisResult _technicalAnalysisResult;
//        public TechnicalAnalysisResult TechnicalAnalysisResult
//        {
//            get { return _technicalAnalysisResult; }
//            set
//            {
//                _technicalAnalysisResult = value;
//                OnPropertyChanged(nameof(TechnicalAnalysisResult));
//            }
//        }
//    }
//}
