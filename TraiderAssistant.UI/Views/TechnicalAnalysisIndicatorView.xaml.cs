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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TechnicalAnalysis.Shared;

namespace TraiderAssistant.UI.Views
{
    /// <summary>
    /// Interaction logic for TechAnalysisIndicatorView.xaml
    /// </summary>
    public partial class TechnicalAnalysisIndicatorView : UserControl
    {
        private ILogger _logger;
        private Line arrow;

        public static readonly DependencyProperty TechnicalAnalysisIndicatorProperty =
            DependencyProperty.Register("TechnicalAnalysisIndicator", typeof(double?), typeof(TechnicalAnalysisIndicatorView), new PropertyMetadata(null, OnTechnicalAnalysisIndicatorChanged));//PropertyMetadata(0.0, OnTechnicalAnalysisIndicatorChanged
        public static readonly DependencyProperty TechnicalAnalysisTextProperty =
            DependencyProperty.Register("TechnicalAnalysisText", typeof(string), typeof(TechnicalAnalysisIndicatorView), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty TechnicalAnalysisNameProperty =
             DependencyProperty.Register("TechnicalAnalysisName", typeof(string), typeof(TechnicalAnalysisIndicatorView), new PropertyMetadata(string.Empty));

        public double? TechnicalAnalysisIndicator
        {
            get { return (double?)GetValue(TechnicalAnalysisIndicatorProperty); }
            set { SetValue(TechnicalAnalysisIndicatorProperty, value); }
        }
        public string TechnicalAnalysisText
        {
            get { return (string)GetValue(TechnicalAnalysisTextProperty); }
            set { SetValue(TechnicalAnalysisTextProperty, value); }
        }
        public string TechnicalAnalysisName
        {
            get { return (string)GetValue(TechnicalAnalysisNameProperty); }
            set { SetValue(TechnicalAnalysisNameProperty, value); }
        }

        private static void OnTechnicalAnalysisIndicatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as TechnicalAnalysisIndicatorView;
            view?.UpdateCanvas();
        }

        public TechnicalAnalysisIndicatorView()
        {
            InitializeComponent();
            this.SizeChanged += TechnicalAnalysisIndicatorView_SizeChanged;

            _logger = SerilogLoggerFactory.CreateLogger<TechnicalAnalysisIndicatorView>();
            _logger.LogInfo("TechnicalAnalysisIndicatorView initialized");
        }


        private void TechnicalAnalysisIndicatorView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateFontSize();
        }

        private void UpdateFontSize()
        {
            double newFontSize = 20; 
            TechnicalAnalysisNameTextBox.FontSize = newFontSize;
            TechnicalAnalysisTextTextBox.FontSize = newFontSize;
        }

        private void UpdateCanvas()
        {
            _logger.LogInfo("UpdateCanvas");
            var canvas = AnalysisCanvas;
            canvas.Children.Clear();

            DrawSectors(canvas);
            DrawArrow(canvas);
        }

        private void DrawSectors(Canvas canvas)
        {
            _logger.LogInfo("DrawSectors");

            // Circle centre
            double centerX = canvas.ActualWidth * 0.5;
            double centerY = canvas.ActualHeight * 0.9;
            double outerRadius = canvas.ActualHeight * 0.75;
            double innerRadius = canvas.ActualHeight * 0.7;


            Brush[] sectorColors = { Brushes.Green, Brushes.LightGreen, Brushes.LightGray, Brushes.Orange, Brushes.Red };
            double sectorAngle = 180 / sectorColors.Length;

            double currentAngle = 0; // Initial angle

            for (int i = 0; i < sectorColors.Length; i++)
            {
                // Creating a sector
                var path = CreateOuterArc(centerX, centerY, outerRadius, innerRadius, currentAngle, currentAngle + sectorAngle);
                path.Fill = sectorColors[i];
                canvas.Children.Add(path);
                // Shift the current angle to the next sector
                currentAngle += sectorAngle;
            }
        }

        private Path CreateOuterArc(double centerX, double centerY, double outerRadius, double innerRadius, double startAngle, double endAngle)
        {
            // Converting angles from degrees to radians
            double startAngleRad = startAngle * Math.PI / 180;
            double endAngleRad = endAngle * Math.PI / 180;

            // Calculate points for the outer arc
            Point outerStartPoint = new Point(
                centerX + outerRadius * Math.Cos(startAngleRad),
                centerY - outerRadius * Math.Sin(startAngleRad)
            );

            Point outerEndPoint = new Point(
                centerX + outerRadius * Math.Cos(endAngleRad),
                centerY - outerRadius * Math.Sin(endAngleRad)
            );

            // Calculate points for the inner arc
            Point innerStartPoint = new Point(
                centerX + innerRadius * Math.Cos(endAngleRad),
                centerY - innerRadius * Math.Sin(endAngleRad)
            );

            Point innerEndPoint = new Point(
                centerX + innerRadius * Math.Cos(startAngleRad),
                centerY - innerRadius * Math.Sin(startAngleRad)
            );

            // Create arc segments
            bool isLargeArc = endAngle - startAngle > 180;

            var outerArc = new ArcSegment
            {
                Point = innerEndPoint,
                Size = new Size(outerRadius, outerRadius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            };

            var innerArc = new ArcSegment
            {
                Point = outerEndPoint,
                Size = new Size(innerRadius, innerRadius),
                SweepDirection = SweepDirection.Counterclockwise,
                IsLargeArc = isLargeArc
            };

            // Create a figure (external arc with closure to internal)
            var pathFigure = new PathFigure
            {
                StartPoint = innerStartPoint,
                IsClosed = true
            };
            pathFigure.Segments.Add(outerArc);  // External arc
            pathFigure.Segments.Add(new LineSegment(outerStartPoint, true)); // Line from outer end to inner beginning
            pathFigure.Segments.Add(innerArc);  // Inner arc

            var path = new Path
            {
                Data = new PathGeometry(new[] { pathFigure })
            };

            return path;
        }

        private void DrawArrow(Canvas canvas)
        {
            _logger.LogInfo("DrawArrow");

            double centerX = canvas.ActualWidth * 0.5;
            double centerY = canvas.ActualHeight * 0.9;
            double radius = canvas.ActualHeight * 0.8;

            // Updating the rotation angle of the arrow depending on the indicator value
            double angle = IndicatorToAngle(TechnicalAnalysisIndicator ?? 0);
            double angleRadian = DegreesToRadians(angle);

            double pointX = radius * Math.Sin(angleRadian);
            pointX = centerX + pointX;

            double pointY = radius * Math.Cos(angleRadian);
            pointY = centerY - pointY;

            arrow = new Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = pointX,
                Y2 = pointY,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
            };

            canvas.Children.Add(arrow);
        }



        double DegreesToRadians(double degrees)
        {
            _logger.LogInfo("DegreesToRadians");
            return degrees == 0 ? 0 : degrees * Math.PI / 180.0;
        }


        /// <summary>
        /// Converts the indicator value to the angle of rotation of the arrow.
        /// The indicator value varies from -100 to 100.
        /// The value -100 corresponds to an angle of -90°, 0 - 0°, 100 corresponds to an angle of 90°.
        /// </summary>
        double IndicatorToAngle(double indicator)
        {
            _logger.LogInfo("IndicatorToAngle");
            double angle = (90 / 100.0) * indicator;
            return angle;
        }

    }
}
