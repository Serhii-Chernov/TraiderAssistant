﻿using System;
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
using TraiderAssistant.Infrastructure.Services;
using TraiderAssistant.UI.ViewModels;

namespace TraiderAssistant.UI.Views
{

    public partial class TechAnalysisView : UserControl
    {
        //private TechAnalysisViewModel viewModel;
        private Line arrow;

        public static readonly DependencyProperty TechnicalAnalysisResultProperty =
            DependencyProperty.Register("TechnicalAnalysisResult", typeof(TechnicalAnalysisResult), typeof(TechAnalysisView), new PropertyMetadata(null, OnTechnicalAnalysisResultChanged));

        public TechnicalAnalysisResult TechnicalAnalysisResult
        {
            get { return (TechnicalAnalysisResult)GetValue(TechnicalAnalysisResultProperty); }
            set { SetValue(TechnicalAnalysisResultProperty, value); }
        }

        public TechAnalysisView()
        {
            //viewModel = new TechAnalysisViewModel();
            InitializeComponent();
        }

        private static void OnTechnicalAnalysisResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as TechAnalysisView;
            view?.UpdateCanvas();
        }

        private void UpdateCanvas()
        {
            var canvas = AnalysisCanvas;
            if (TechnicalAnalysisResult != null)
            {
                canvas.Children.Clear();

                DrawSectors(canvas);
                DrawArrow(canvas);

            }
        }

        private void DrawSectors(Canvas canvas)
        {
            /// <summary>
            /// Расхардкодить.
            /// </summary>
            // Центр круга
            double centerX = canvas.Width*0.5;//100
            double centerY = canvas.Height*0.9;//100
            double outerRadius = canvas.Height * 0.75; // Радиус внешней дуги
            double innerRadius = canvas.Height * 0.65; // Радиус внутренней дуги

            // Углы секторов (в градусах)
            //double[] sectorAngles = { 36, 36, 36, 36, 36 }; // 5 равных секторов по 36°
            Brush[] sectorColors = { Brushes.Green, Brushes.LightGreen, Brushes.LightGray, Brushes.Orange , Brushes.Red };
            double sectorAngle = 180 / sectorColors.Length;

            double currentAngle = 0; // Начальный угол

            for (int i = 0; i < sectorColors.Length; i++)
            {
                // Создаем сектор
                var path = CreateOuterArc(centerX, centerY, outerRadius, innerRadius, currentAngle, currentAngle + sectorAngle);
                path.Fill = sectorColors[i];
                canvas.Children.Add(path);
                // Смещаем текущий угол для следующего сектора
                currentAngle += sectorAngle;
            }
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

        private void DrawArrow(Canvas canvas)
        {
            /// <summary>
            /// Расхардкодить.
            /// </summary>
            double centerX = canvas.Width * 0.5; //100
            double centerY = canvas.Height * 0.9; //100
            double radius = canvas.Height* 0.8; //90

            // Обновляем угол поворота стрелки в зависимости от значения индикатора
            double angle = IndicatorToAngle(TechnicalAnalysisResult.Indicator);
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
                StrokeThickness = 2
            };


            canvas.Children.Add(arrow);
        }

        double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Преобразует значение индикатора в угол поворота стрелки.
        /// Значение индикатора варьируется от -100 до 100.
        /// Значение -100 соответствует углу -90°, 0 - 0°, 100 соответствует углу 90°.
        /// </summary>
        /// <returns></returns>
        double IndicatorToAngle(double indicator)
        {
            double angle = 90 / 100.0 * indicator;
            return angle;
        }
    }
}
