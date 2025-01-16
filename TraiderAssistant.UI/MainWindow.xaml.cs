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
using TraiderAssistant.UI.Views;

namespace TraiderAssistant.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void BitcoinButton_Click(object sender, RoutedEventArgs e)
        {
            // Загружаем BitcoinChartView в Frame
            MainContent.Navigate(new BitcoinChartView());
        }

        private void NotcoinButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Navigate(new NotcoinChartView());
        }
    }
}