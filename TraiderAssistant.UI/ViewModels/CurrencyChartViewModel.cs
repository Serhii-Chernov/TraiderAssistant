using Binance.Net.Enums;
using GalaSoft.MvvmLight.CommandWpf;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TechnicalAnalysis.Shared;
using System.Windows.Threading;

namespace TraiderAssistant.UI.ViewModels
{
    public class CurrencyChartViewModel : INotifyPropertyChanged
    {
        private readonly ILogger _logger;

        private ChartValues<decimal> _lineValues;
        private ChartValues<OhlcPoint> _candleValues;
        private string _currentChartType = string.Empty;
        public SeriesCollection Series { get; set; }
        public ObservableCollection<string> Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }
        TechnicalAnalysisClient _technicalAnalysisClient;
        public CurrencyPair CurrencyPair { get; set; }
        
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

        private readonly DispatcherTimer _updateTimer;
        private bool _isAutoUpdating;
        private DateTime _startTime;
        private DateTime _endTime;
        public ICommand SetChartPeriodCommand { get; }
        public ICommand EnableAutoUpdateCommand { get; }

        public CurrencyChartViewModel(TechnicalAnalysisClient technicalAnalysisClient, CurrencyPair currencyPair)
        {
            _logger = SerilogLoggerFactory.CreateLogger<CurrencyChartViewModel>();
            _logger.LogInfo("CurrencyChartViewModel initialized");

            _technicalAnalysisClient = technicalAnalysisClient;
            CurrencyPair = currencyPair;

            ChartTypes = new List<string> { "Line", "Candle" };
            SelectedChartType = "Line"; 

            YFormatter = value => value.ToString("N5"); 

            SetTechnicalAnalysisKlineIntervalCommand = new RelayCommand<KlineInterval>(interval => TechnicalAnalysisKlineInterval = interval);

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _updateTimer.Tick += async (s, e) => await RefreshChart();

            SetChartPeriodCommand = new RelayCommand<string>(async (period) => await LoadSelectedChartPeriod(period));
            EnableAutoUpdateCommand = new RelayCommand(async () => await ToggleAutoUpdate());

            InitializeTechnicalAnalysis(DateTime.UtcNow).ConfigureAwait(true);
        }

        private async Task LoadSelectedChartPeriod(string period)
        {
            _isAutoUpdating = false;
            _updateTimer.Stop();

            int days = period switch
            {
                "1D" => 1,
                "1W" => 7,
                "1M" => 30,
                "1Y" => 365,
                _ => 1
            };

            _startTime = DateTime.UtcNow.AddDays(-days);
            _endTime = DateTime.UtcNow;

            await LoadDataAsync(_startTime, _endTime);
        }

        private async Task ToggleAutoUpdate()
        {
            _isAutoUpdating = !_isAutoUpdating;

            if (_isAutoUpdating)
            {
                _updateTimer.Start();
                await RefreshChart();
            }
            else
            {
                _updateTimer.Stop();
            }
        }

        private async Task RefreshChart()
        {
            if (!_isAutoUpdating) return;

            _startTime = DateTime.UtcNow.AddHours(-1);
            _endTime = DateTime.UtcNow;

            await LoadDataAsync(_startTime, _endTime);
        }

        private async void OnTimePeriodSelected(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int days = int.Parse(button.Tag.ToString());

            DateTime startTime = DateTime.UtcNow.AddDays(-days);
            DateTime endTime = DateTime.UtcNow;

            _isAutoUpdating = false;
            _updateTimer.Stop();

            await LoadDataAsync(startTime, endTime);
        }

        //private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
        //{
        //    var data = await _technicalAnalysisClient.GetChartDataAsync(CurrencyPair, startTime, endTime);
        //    var prices = data.Select(k => k.ClosePrice).ToArray();
        //    var dates = data.Select(k => k.CloseTime.ToString("dd/MM/yyyy")).ToArray();

        //    StringBuilder title = new StringBuilder();
        //    var periodPersent = Math.Round((prices.Last() - prices.First()) / prices.First() * 100,2);
        //    string sign = periodPersent > 0 ? "+" : "";
        //    var roundedValue = Math.Round(prices.Last(), 5);
        //    title.Append($"{CurrencyPair.ToString()}  {roundedValue.ToString()}  {sign}{periodPersent}%");
        //    Series = new SeriesCollection();

        //    switch (SelectedChartType)
        //    {
        //        case "Line":

        //            Series.Add(new LineSeries
        //            {

        //                Title = title.ToString(),
        //                Values = new ChartValues<decimal>(prices),
        //                PointGeometry = null, // Remove circules
        //                PointGeometrySize = 0, // Set the size of the circles to 0
        //            });
        //            break;

        //        case "Candle":
        //            var candleData = data.Select(k => new OhlcPoint((double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice)).ToArray();
        //            Series.Add(new CandleSeries
        //            {
        //                Title = title.ToString(),
        //                Values = new ChartValues<OhlcPoint>(candleData)
        //            });
        //            break;
        //    }

        //    Labels = new ObservableCollection<string>(dates);
        //    OnPropertyChanged(nameof(Series));
        //    OnPropertyChanged(nameof(Labels));
        //}
        private async Task LoadDataAsync(DateTime startTime, DateTime endTime)
        {
            var data = await _technicalAnalysisClient.GetChartDataAsync(CurrencyPair, startTime, endTime);
            if (data == null || !data.Any()) return;

            var prices = data.Select(k => k.ClosePrice).ToArray();

            // Улучшение: если включен режим автообновления, показываем часы и минуты, иначе - дату
            var dates = data.Select(k => _isAutoUpdating ? k.CloseTime.ToString("HH:mm:ss") : k.CloseTime.ToString("dd/MM/yyyy")).ToArray();

            StringBuilder title = new StringBuilder();
            var periodPersent = Math.Round((prices.Last() - prices.First()) / prices.First() * 100, 2);
            string sign = periodPersent > 0 ? "+" : "";
            var roundedValue = Math.Round(prices.Last(), 5);
            string newTitle = $"{CurrencyPair}  {roundedValue}  {sign}{periodPersent}%";

            // 1. СОЗДАЕМ ГРАФИКИ ТОЛЬКО ОДИН РАЗ (или если пользователь переключил Line <-> Candle)
            if (Series == null || _currentChartType != SelectedChartType)
            {
                _lineValues = new ChartValues<decimal>();
                _candleValues = new ChartValues<OhlcPoint>();
                Series = new SeriesCollection();

                if (SelectedChartType == "Line")
                {
                    Series.Add(new LineSeries
                    {
                        Title = newTitle,
                        Values = _lineValues,
                        PointGeometry = null,
                        PointGeometrySize = 0,
                    });
                }
                else if (SelectedChartType == "Candle")
                {
                    Series.Add(new CandleSeries
                    {
                        Title = newTitle,
                        Values = _candleValues
                    });
                }

                _currentChartType = SelectedChartType;
                OnPropertyChanged(nameof(Series));
            }
            else
            {
                // Если график уже существует, приводим его к типу Series из WPF и спокойно меняем заголовок
                if (Series[0] is LiveCharts.Wpf.Series existingSeries)
                {
                    existingSeries.Title = newTitle;
                }
            }

            // 2. БЫСТРОЕ ОБНОВЛЕНИЕ ДАННЫХ (БЕЗ ПЕРЕСОЗДАНИЯ UI)
            if (SelectedChartType == "Line")
            {
                _lineValues.Clear();
                _lineValues.AddRange(prices);
            }
            else if (SelectedChartType == "Candle")
            {
                var candleData = data.Select(k => new OhlcPoint((double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice));
                _candleValues.Clear();
                _candleValues.AddRange(candleData);
            }

            // Метки оси X обновляем
            Labels = new ObservableCollection<string>(dates);
            OnPropertyChanged(nameof(Labels));
        }

        private async Task InitializeTechnicalAnalysis(DateTime endTime, KlineInterval klineInterval = KlineInterval.OneMinute)
        {
            TechnicalAnalysisResult = await _technicalAnalysisClient.GetTechnicalAnalysisAsync(CurrencyPair, endTime, klineInterval);
            _logger.LogInfo($"TechnicalAnalysisResult: OscillatorIndicator={TechnicalAnalysisResult.OscillatorIndicator}," +
                $" MAIndicator={TechnicalAnalysisResult.MAIndicator}, GeneralIndicator={TechnicalAnalysisResult.GeneralIndicator}");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
