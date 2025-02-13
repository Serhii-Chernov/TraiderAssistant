//using Binance.Net.Enums;
//using Binance.Net.Objects.Models.Spot;
//using LiveCharts.Wpf;
//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Windows;
//using TraiderAssistant.Infrastructure.Services;
//using TraiderAssistant.Infrastructure.Services.TechnicalAnalysis;

//namespace TraiderAssistant.UI.ViewModels
//{
//    public class TechAnalysisViewModel
//    {
//        //private double indicatorValue;

//        //public double IndicatorValue
//        //{
//        //    get => indicatorValue;
//        //    set
//        //    {
//        //        indicatorValue = value;
//        //        OnPropertyChanged();
//        //        //UpdateIndicator();
//        //    }
//        //}

//        //TechnicalAnalysisService techAnalysisService;

//        public TechAnalysisViewModel()
//        {
//            //techAnalysisService = new TechnicalAnalysisService();
//            //IndicatorValue = 0; // Нейтрально
//        }

//        public async Task<TechnicalAnalysisResult> PerformTechnicalAnalysis(CurrencyPair currencyPair, DateTime endTime, KlineInterval klineInterval)
//        {
//            TechnicalAnalysisClient technicalAnalysisClient = new TechnicalAnalysisClient();
//            var analysis = await technicalAnalysisClient.GetTechnicalAnalysisAsync(currencyPair, endTime, klineInterval);
//            //var data2 = await _binanceService.GetChartDataForIndicatorsAsync(currencyPair, endTime);
//            //return techAnalysisService.PerformTechnicalAnalysis(data);
//            return analysis;
//        }

//        //public event PropertyChangedEventHandler PropertyChanged;
//        //protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
//        //{
//        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        //}
//    }
//}

