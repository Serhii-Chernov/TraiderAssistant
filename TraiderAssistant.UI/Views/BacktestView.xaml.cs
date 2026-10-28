using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TraiderAssistant.UI.Views
{
    public partial class BacktestView : Page
    {
        private readonly HttpClient _httpClient = new HttpClient();
        public Func<double, string> YFormatter { get; set; }

        public BacktestView()
        {
            InitializeComponent();

            // Настройка форматирования оси Y (в долларах)
            YFormatter = value => $"$ {value:N0}";
            DataContext = this;

            // Заполняем выпадающие списки (замени на свои Enum, если нужно)
            ComboCurrency.ItemsSource = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "DOGEUSDT" };
            ComboCurrency.SelectedIndex = 1; // ETHUSDT по умолчанию (наш лидер)

            ComboTimeframe.ItemsSource = new[] { "OneMinute", "FiveMinutes", "FifteenMinutes", "ThirtyMinutes", "OneHour", "FourHour", "OneDay" };
            ComboTimeframe.SelectedIndex = 6; // OneDay по умолчанию
        }

        private async void BtnRunBacktest_Click(object sender, RoutedEventArgs e)
        {
            if (ComboCurrency.SelectedItem == null || ComboTimeframe.SelectedItem == null) return;

            string asset = ComboCurrency.SelectedItem.ToString();
            string tf = ComboTimeframe.SelectedItem.ToString();
            decimal deposit = decimal.Parse(TxtDeposit.Text);

            BtnRunBacktest.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;

            try
            {
                // ЗАМЕНИ ПОРТ НА ТОТ, НА КОТОРОМ КРУТИТСЯ ТВОЙ WebApi (например, 7010 или 5001)
                string apiUrl = $"https://localhost:7010/api/Backtest/RunSingle?pair={asset}&interval={tf}&initialDeposit={deposit}";

                var response = await _httpClient.GetStringAsync(apiUrl);
                var result = JsonConvert.DeserializeObject<BacktestResultDto>(response);

                UpdateUI(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Сбой API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRunBacktest.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUI(BacktestResultDto result)
        {
            // 1. Обновляем красивые карточки с метриками
            TxtTotalProfit.Text = $"{result.ProfitPercentage:F2} %";
            TxtTotalProfit.Foreground = result.ProfitPercentage > 0 ? Brushes.ForestGreen : Brushes.Red;

            double winRate = result.TotalTrades > 0 ? ((double)result.WinningTrades / result.TotalTrades) * 100 : 0;
            TxtWinRate.Text = $"{winRate:F1} %";

            TxtDrawdown.Text = $"-{result.MaxDrawdown:F2} %";
            TxtProfitFactor.Text = $"{result.ProfitFactor:F2}";
            TxtSharpe.Text = $"{result.SharpeRatio:F2}";

            // 2. РИСУЕМ ГРАФИК EQUITY CURVE
            var balanceValues = new ChartValues<double>();
            var labels = new List<string>();

            // Начальная точка
            balanceValues.Add((double)result.InitialBalance);
            labels.Add("Start");

            double currentBalance = (double)result.InitialBalance;

            // Добавляем результаты каждой закрытой сделки на график
            foreach (var trade in result.Trades.Where(t => t.Type != "Signal")) // Берем только выходы из сделок
            {
                currentBalance += (double)trade.Profit;
                balanceValues.Add(currentBalance);
                labels.Add(trade.ExitTime.ToString("dd.MM.yy")); // Или hh:mm для мелких таймфреймов
            }

            // Настраиваем красивую зеленую линию с градиентом
            var lineSeries = new LineSeries
            {
                Title = "Balance",
                Values = balanceValues,
                Stroke = new SolidColorBrush(Color.FromRgb(46, 125, 50)), // Темно-зеленый
                StrokeThickness = 2,
                PointGeometry = null, // Убираем точки, чтобы линия была гладкой
                Fill = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80)) // Полупрозрачный светло-зеленый низ
            };

            EquityChart.Series = new SeriesCollection { lineSeries };
            AxisX.Labels = labels;
        }
    }

    // DTO (Копия твоей серверной модели для Десериализации)
    public class BacktestResultDto
    {
        public decimal InitialBalance { get; set; }
        public decimal ProfitPercentage { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal SharpeRatio { get; set; }
        public List<TradeDto> Trades { get; set; } = new List<TradeDto>();
    }

    public class TradeDto
    {
        public DateTime ExitTime { get; set; }
        public decimal Profit { get; set; }
        public string Type { get; set; }
    }
}