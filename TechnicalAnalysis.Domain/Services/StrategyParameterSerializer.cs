using Newtonsoft.Json;
using TechnicalAnalysis.Domain.MLModels;

namespace TechnicalAnalysis.Domain.Services;

internal static class StrategyParameterSerializer
{
    internal static string Serialize(OptimizationInput parameters) =>
        JsonConvert.SerializeObject(ToSchema(parameters));

    internal static WeightsJsonSchema ToSchema(OptimizationInput parameters) => new()
    {
        OscillatorGroupWeight = (decimal)parameters.OscillatorGroupWeight,
        MaGroupWeight = (decimal)parameters.MaGroupWeight,
        Oscillators = new Dictionary<string, decimal>
        {
            ["RSI"] = (decimal)parameters.RsiWeight,
            ["StochasticK"] = (decimal)parameters.StochasticKWeight,
            ["CCI"] = (decimal)parameters.CciWeight,
            ["ADX"] = (decimal)parameters.AdxWeight,
            ["AO"] = (decimal)parameters.AoWeight,
            ["Momentum"] = (decimal)parameters.MomentumWeight,
            ["MACD"] = (decimal)parameters.MacdWeight,
            ["UltimateOscillator"] = (decimal)parameters.UltimateWeight,
            ["Williams%R"] = (decimal)parameters.WilliamsRWeight,
            ["BullBearPower"] = (decimal)parameters.BullBearWeight,
            ["StochasticRSI"] = (decimal)parameters.StochasticRsiWeight
        },
        MAs = new Dictionary<string, decimal>
        {
            ["SMA(10)"] = (decimal)parameters.Sma10Weight,
            ["EMA(10)"] = (decimal)parameters.Ema10Weight,
            ["SMA(20)"] = (decimal)parameters.Sma20Weight,
            ["EMA(20)"] = (decimal)parameters.Ema20Weight,
            ["SMA(30)"] = (decimal)parameters.Sma30Weight,
            ["EMA(30)"] = (decimal)parameters.Ema30Weight,
            ["SMA(50)"] = (decimal)parameters.Sma50Weight,
            ["EMA(50)"] = (decimal)parameters.Ema50Weight,
            ["SMA(100)"] = (decimal)parameters.Sma100Weight,
            ["EMA(100)"] = (decimal)parameters.Ema100Weight,
            ["SMA(200)"] = (decimal)parameters.Sma200Weight,
            ["EMA(200)"] = (decimal)parameters.Ema200Weight,
            ["HMA(9)"] = (decimal)parameters.Hma9Weight
        },
        BuyThreshold = (int)parameters.BuyThreshold,
        SellThreshold = (int)parameters.SellThreshold,
        StopLoss = (decimal)parameters.StopLoss,
        TakeProfit = (decimal)parameters.TakeProfit
    };
}