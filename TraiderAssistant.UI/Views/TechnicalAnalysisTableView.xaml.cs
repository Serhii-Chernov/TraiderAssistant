using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using TechnicalAnalysis.Shared;

//using TraiderAssistant.Infrastructure.Services.TechnicalAnalysis;
using TraiderAssistant.UI.ViewModels;

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
            // Очистите предыдущие элементы
            TableGrid.Children.Clear();
            TableGrid.RowDefinitions.Clear();
            TableGrid.ColumnDefinitions.Clear();

            // Добавьте заголовки столбцов
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Добавьте заголовок строки
            TableGrid.RowDefinitions.Add(new RowDefinition());
            AddCellToGrid("Name", 0, 0, true);
            AddCellToGrid("Value", 1, 0, true);
            AddCellToGrid("Action", 2, 0, true);

            // Добавьте данные
            int row = 1;
            foreach (var item in TechnicalAnalysisTableData)
            {
                TableGrid.RowDefinitions.Add(new RowDefinition());

                AddCellToGrid(item.Name, 0, row);
                var roundedValue = Math.Round(item.Value, 5);
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
                Margin = new Thickness(20),
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



        //private void UpdateCanvas()
        //{
        //    // Очистите предыдущие элементы
        //    TableGrid.Children.Clear();
        //    TableGrid.RowDefinitions.Clear();
        //    TableGrid.ColumnDefinitions.Clear();

        //    // Добавьте заголовки столбцов
        //    TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
        //    TableGrid.ColumnDefinitions.Add(new ColumnDefinition());
        //    TableGrid.ColumnDefinitions.Add(new ColumnDefinition());

        //    // Добавьте заголовок строки
        //    TableGrid.RowDefinitions.Add(new RowDefinition());
        //    TableGrid.Children.Add(new TextBlock() { Text = "Name", FontWeight = FontWeights.Bold, Margin = new Thickness(30) });
        //    Grid.SetColumn(TableGrid.Children[TableGrid.Children.Count - 1], 0);
        //    Grid.SetRow(TableGrid.Children[TableGrid.Children.Count - 1], 0);

        //    TableGrid.Children.Add(new TextBlock() { Text = "Value", FontWeight = FontWeights.Bold, Margin = new Thickness(30) });
        //    Grid.SetColumn(TableGrid.Children[TableGrid.Children.Count - 1], 1);
        //    Grid.SetRow(TableGrid.Children[TableGrid.Children.Count - 1], 0);

        //    TableGrid.Children.Add(new TextBlock() { Text = "Action", FontWeight = FontWeights.Bold, Margin = new Thickness(30) });
        //    Grid.SetColumn(TableGrid.Children[TableGrid.Children.Count - 1], 2);
        //    Grid.SetRow(TableGrid.Children[TableGrid.Children.Count - 1], 0);

        //    // Добавьте данные
        //    int row = 1;
        //    foreach (var item in TechnicalAnalysisTableData)
        //    {
        //        TableGrid.RowDefinitions.Add(new RowDefinition());

        //        var nameTextBlock = new TextBlock() { Text = item.Name, Margin = new Thickness(20, 10,0,0) };
        //        Grid.SetColumn(nameTextBlock, 0);
        //        Grid.SetRow(nameTextBlock, row);
        //        TableGrid.Children.Add(nameTextBlock);

        //        var roundedValue = Math.Round(item.Value, 5);
        //        var valueTextBlock = new TextBlock() { Text = roundedValue.ToString(), Margin = new Thickness(20, 10, 0, 0) };
        //        Grid.SetColumn(valueTextBlock, 1);
        //        Grid.SetRow(valueTextBlock, row);
        //        TableGrid.Children.Add(valueTextBlock);

        //        var actionTextBlock = new TextBlock() { Text = item.Action, Margin = new Thickness(20, 10, 0, 0) };
        //        Grid.SetColumn(actionTextBlock, 2);
        //        Grid.SetRow(actionTextBlock, row);
        //        TableGrid.Children.Add(actionTextBlock);

        //        row++;
        //    }
        //}
    }
}
