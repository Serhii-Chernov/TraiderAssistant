using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TechnicalAnalysis.Shared;

namespace TraiderAssistant.UI.Views
{
    public partial class TechnicalAnalysisTableView : UserControl
    {
        public static readonly DependencyProperty TechnicalAnalysisTableDataProperty =
            DependencyProperty.Register(
            nameof(TechnicalAnalysisTableData),
            typeof(List<TechnicalAnalysisNameValueActionStruct>),
            typeof(TechnicalAnalysisTableView),
            new PropertyMetadata(null, TechnicalAnalysisDataChanged));

        public List<TechnicalAnalysisNameValueActionStruct> TechnicalAnalysisTableData
        {
            get { return (List<TechnicalAnalysisNameValueActionStruct>)GetValue(TechnicalAnalysisTableDataProperty); }
            set { SetValue(TechnicalAnalysisTableDataProperty, value); }
        }
        
        public static readonly DependencyProperty TechnicalAnalysisTableNameProperty =
            DependencyProperty.Register(nameof(TechnicalAnalysisTableName), typeof(string),
            typeof(TechnicalAnalysisTableView), new PropertyMetadata(string.Empty));

        public string TechnicalAnalysisTableName
        {
            get { return (string)GetValue(TechnicalAnalysisTableNameProperty); }
            set { SetValue(TechnicalAnalysisTableNameProperty, value); }
        }

        public TechnicalAnalysisTableView()
        {
            InitializeComponent();
        }
        
        private static void TechnicalAnalysisDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as TechnicalAnalysisTableView;
            view?.UpdateCanvas();
        }
        private void UpdateCanvas()
        {
            // Clear previous elements
            TableGrid.Children.Clear();
            TableGrid.RowDefinitions.Clear();
            TableGrid.ColumnDefinitions.Clear();

            // Add column headings
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Add row headings
            TableGrid.RowDefinitions.Add(new RowDefinition());
            AddCellToGrid("Name", 0, 0, true);
            AddCellToGrid("Value", 1, 0, true);
            AddCellToGrid("Action", 2, 0, true);

            // Add data
            int row = 1;
            foreach (var item in TechnicalAnalysisTableData)
            {
                TableGrid.RowDefinitions.Add(new RowDefinition());

                AddCellToGrid(item.Name, 0, row);
                var roundedValue = Math.Round(item.Value, 2);
                AddCellToGrid(roundedValue.ToString(), 1, row);
                AddCellToGrid(item.Action, 2, row);

                row++;
            }
        }

        private void AddCellToGrid(string text, int column, int row, bool isHeader = false)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Margin = new Thickness(10),
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Child = textBlock
            };

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            TableGrid.Children.Add(border);
        }
    }
}
