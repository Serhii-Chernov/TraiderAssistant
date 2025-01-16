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
using TraiderAssistant.UI.Converters;
using TraiderAssistant.UI.ViewModels;

namespace TraiderAssistant.UI.Views
{
    /// <summary>
    /// Interaction logic for TechAnalysisView.xaml
    /// </summary>
    public partial class TechAnalysisView : UserControl
    {
        private TechAnalysisViewModel viewModel;
        public TechAnalysisView()
        {
            InitializeComponent();
            viewModel = new TechAnalysisViewModel();
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = viewModel;
            //DataContext = new TechAnalysisViewModel();

            // Создаем Canvas
            var canvas = new Canvas
            {
                Width = 300,
                Height = 300
            };
            this.Content = canvas;

            // Центр круга
            double centerX = 100;
            double centerY = 100;
            double outerRadius = 75; // Радиус внешней дуги
            double innerRadius = 65; // Радиус внутренней дуги

            // Углы секторов (в градусах)
            double[] sectorAngles = { 36, 36, 36, 36, 36 }; // 5 равных секторов по 36°
            Brush[] sectorColors = { Brushes.Red, Brushes.Orange, Brushes.LightGray, Brushes.LightGreen, Brushes.Green };

            double currentAngle = 0; // Начальный угол

            for (int i = 0; i < sectorAngles.Length; i++)
            {
                // Создаем сектор
                var path = CreateOuterArc(centerX, centerY, outerRadius, innerRadius, currentAngle, currentAngle + sectorAngles[i]);
                path.Fill = sectorColors[i];
                //canvas.Children.Add(path);
                // Смещаем текущий угол для следующего сектора
                currentAngle += sectorAngles[i];
            }

            var arrow = CreateArrow();
            //canvas.Children.Add(arrow);

            ((TechAnalysisViewModel)DataContext).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TechAnalysisViewModel.IndicatorValue))
                {
                    MessageBox.Show($"TechAnalysisView. IndicatorValue:{((TechAnalysisViewModel)s).IndicatorValue}");
                    // Обновление представления
                    arrow.X2 = 100 + 50 * Math.Cos(((TechAnalysisViewModel)s).IndicatorValue * Math.PI / 180);
                    arrow.Y2 = 100 - 50 * Math.Sin(((TechAnalysisViewModel)s).IndicatorValue * Math.PI / 180);
                    arrow.InvalidateVisual();
                }
            };
        }

        private Line CreateArrow()
        {
            var arrow = new Line
            {
                X1 = 100,
                Y1 = 100,
                X2 = 35,
                Y2 = 100,
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };

            var binding = new Binding("IndicatorValue")
            {
                Source = DataContext,
                Converter = new IndicatorValueToAngleConverter()
            };

            var rotateTransform = new RotateTransform();
            arrow.RenderTransform = rotateTransform;
            arrow.RenderTransformOrigin = new Point(1, 1);//(0.5, 0.5)

            BindingOperations.SetBinding(rotateTransform, RotateTransform.AngleProperty, binding);

            return arrow;
        }

        private Path CreateOuterArc(double centerX, double centerY, double outerRadius, double innerRadius, double startAngle, double endAngle)
        {
            // Переводим углы из градусов в радианы
            double startAngleRad = startAngle * Math.PI / 180;
            double endAngleRad = endAngle * Math.PI / 180;

            // Вычисляем точки для внешней дуги
            Point outerStartPoint = new Point(
                centerX + outerRadius * Math.Cos(startAngleRad),
                centerY - outerRadius * Math.Sin(startAngleRad)
            );

            Point outerEndPoint = new Point(
                centerX + outerRadius * Math.Cos(endAngleRad),
                centerY - outerRadius * Math.Sin(endAngleRad)
            );

            // Вычисляем точки для внутренней дуги
            Point innerStartPoint = new Point(
                centerX + innerRadius * Math.Cos(endAngleRad),
                centerY - innerRadius * Math.Sin(endAngleRad)
            );

            Point innerEndPoint = new Point(
                centerX + innerRadius * Math.Cos(startAngleRad),
                centerY - innerRadius * Math.Sin(startAngleRad)
            );

            // Создаем сегменты дуг
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

            // Создаем фигуру (внешняя дуга с замыканием на внутреннюю)
            var pathFigure = new PathFigure
            {
                StartPoint = innerStartPoint,
                IsClosed = true
            };
            pathFigure.Segments.Add(outerArc);  // Внешняя дуга
            pathFigure.Segments.Add(new LineSegment(outerStartPoint, true)); // Линия от внешнего конца к внутреннему началу
            pathFigure.Segments.Add(innerArc);  // Внутренняя дуга

            // Создаем путь
            var path = new Path
            {
                Data = new PathGeometry(new[] { pathFigure })
            };

            return path;
        }
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TechAnalysisViewModel.IndicatorValue))
            {
                // Выполнить действия при изменении SelectedChartType
                MessageBox.Show($"IndicatorValue изменено на: {viewModel.IndicatorValue}");
            }
        }
    }
}
