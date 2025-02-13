using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TechnicalAnalysis.Shared;
using TraiderAssistant.UI.ViewModels;

namespace TraiderAssistant.UI.Views
{
    /// <summary>
    /// Interaction logic for CurrencyChartView.xaml
    /// </summary>
    public partial class CurrencyChartView : Page
    {
        public CurrencyChartView(CurrencyPair currencyPair)
        {
            //var techAnalysisViewModel = new TechAnalysisViewModel();
            TechnicalAnalysisClient technicalAnalysisClient = new TechnicalAnalysisClient();
            DataContext = new CurrencyChartViewModel(technicalAnalysisClient, currencyPair);
            InitializeComponent();
        }
    }
}
