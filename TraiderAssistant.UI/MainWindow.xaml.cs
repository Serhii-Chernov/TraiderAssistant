using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
//using TraiderAssistant.Infrastructure.Services;
using TraiderAssistant.UI.Views;
//using TraiderAssistant.Infrastructure;
using TechnicalAnalysis.Shared;

namespace TraiderAssistant.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ILogger _logger;
        public MainWindow()
        {
            InitializeComponent();
            SerilogLoggerFactory.Configure();
            _logger = SerilogLoggerFactory.CreateLogger<MainWindow>();
            _logger.LogInfo("MainWindow initialized");
        }
        private void BTCButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: BTCUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.BTCUSDT));
        }
        private void ETHButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: ETHUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.ETHUSDT));
        }
        private void BNBButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: BNBUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.BNBUSDT));
        }
        private void XRPButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: XPRUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.XRPUSDT));
        }
        private void SOLButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: SOLUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.SOLUSDT));
        }
        private void KAITOButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: KAITOUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.KAITOUSDT));
        }
        private void DOGEButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: DOGEUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.DOGEUSDT));
        }
        private void LTCButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("MainContent: LTCUSDT");
            MainContent.Navigate(new CurrencyChartView(CurrencyPair.LTCUSDT));
        }
    }
}