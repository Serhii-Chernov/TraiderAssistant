using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using TraiderAssistant.Infrastructure.Services.TechnicalAnalysis;
using TraiderAssistant.UI.ViewModels;

namespace TraiderAssistant.UI.Views
{

    public partial class TechnicalAnalysisView : UserControl
    {
        //private TechAnalysisViewModel viewModel;
        //private Line arrow;

        //public static readonly DependencyProperty TechnicalAnalysisIndicatorProperty =
        //    DependencyProperty.Register("TechnicalAnalysisIndicator", typeof(double), typeof(TechAnalysisView), new PropertyMetadata(0.0, OnTechnicalAnalysisIndicatorChanged));
        public static readonly DependencyProperty TechnicalAnalysisOscillatorsProperty =
            DependencyProperty.Register("TechnicalAnalysisOscillators", typeof(List<TechnicalAnalysisNameValueActionStruct>), typeof(TechnicalAnalysisView), new PropertyMetadata(null, TechnicalAnalysisResultChanged));

        public static readonly DependencyProperty TechnicalAnalysisMovingAveragesProperty =
            DependencyProperty.Register("TechnicalAnalysisMovingAverages", typeof(List<TechnicalAnalysisNameValueActionStruct>), typeof(TechnicalAnalysisView), new PropertyMetadata(null, TechnicalAnalysisResultChanged));

        public TechnicalAnalysisResult TechnicalAnalysisOscillators
        {
            get { return (TechnicalAnalysisResult)GetValue(TechnicalAnalysisOscillatorsProperty); }
            set { SetValue(TechnicalAnalysisOscillatorsProperty, value); }
        }

        public TechnicalAnalysisResult TechnicalAnalysisMovingAverages
        {
            get { return (TechnicalAnalysisResult)GetValue(TechnicalAnalysisMovingAveragesProperty); }
            set { SetValue(TechnicalAnalysisMovingAveragesProperty, value); }
        }


        public TechnicalAnalysisView()
        {
            //viewModel = new TechAnalysisViewModel();
            InitializeComponent();
        }

        private static void TechnicalAnalysisResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as TechnicalAnalysisView;
            //view?.UpdateCanvas();
        }

    }
}
