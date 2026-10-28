using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services.Backtesting;

/// <summary>
/// Runs a deterministic long-only simulation without external service or persistence dependencies.
/// </summary>
internal static class BacktestEngine
{
    private enum PendingOrder
    {
        None,
        Enter,
        Exit
    }

    /// <summary>
    /// Simulates trades using signals from closed candles and execution at the next candle open.
    /// </summary>
    internal static BacktestResult Run(
        IReadOnlyList<BacktestCandleSnapshot> candles,
        int simulationStartIndex,
        decimal initialDeposit,
        BacktestSettings settings,
        bool trackTrades)
    {
        ValidateInputs(candles, simulationStartIndex, initialDeposit, settings);

        var result = new BacktestResult
        {
            InitialBalance = initialDeposit,
            Trades = new List<TradeRecord>()
        };

        decimal cashBalance = initialDeposit;
        decimal cryptoAmount = 0m;
        decimal balanceBeforeTrade = initialDeposit;
        decimal entryPrice = 0m;
        DateTime entryTime = default;
        decimal peakEquity = initialDeposit;
        decimal maxDrawdown = 0m;
        decimal grossProfit = 0m;
        decimal grossLoss = 0m;
        int totalTrades = 0;
        int winningTrades = 0;
        int losingTrades = 0;
        PendingOrder pendingOrder = PendingOrder.None;
        var dailyClosingEquity = new SortedDictionary<DateTime, decimal>();

        void ClosePosition(decimal exitPrice, DateTime exitTime, string tradeType)
        {
            cashBalance = cryptoAmount * exitPrice * (1 - settings.TradingFee);
            cryptoAmount = 0m;

            decimal profit = cashBalance - balanceBeforeTrade;
            decimal profitPercent = balanceBeforeTrade == 0m
                ? 0m
                : profit / balanceBeforeTrade * 100m;

            totalTrades++;
            if (profit > 0m)
            {
                winningTrades++;
                grossProfit += profit;
            }
            else
            {
                losingTrades++;
                grossLoss += Math.Abs(profit);
            }

            if (trackTrades)
            {
                result.Trades.Add(new TradeRecord
                {
                    EntryTime = entryTime,
                    EntryPrice = entryPrice,
                    ExitTime = exitTime,
                    ExitPrice = exitPrice,
                    Profit = profit,
                    ProfitPercent = profitPercent,
                    Type = tradeType,
                    EntryIndicatorsJson = "{}"
                });
            }
        }

        for (int index = simulationStartIndex; index < candles.Count; index++)
        {
            var candle = candles[index];

            // Orders are created only after the previous candle closes.
            if (pendingOrder == PendingOrder.Enter && cryptoAmount == 0m)
            {
                entryPrice = candle.OpenPrice;
                entryTime = candle.OpenTime;
                balanceBeforeTrade = cashBalance;
                cryptoAmount = cashBalance / entryPrice * (1 - settings.TradingFee);
                cashBalance = 0m;
            }
            else if (pendingOrder == PendingOrder.Exit && cryptoAmount > 0m)
            {
                ClosePosition(candle.OpenPrice, candle.OpenTime, "Signal");
            }

            pendingOrder = PendingOrder.None;

            if (cryptoAmount > 0m)
            {
                decimal stopPrice = entryPrice * (1 - settings.StopLoss);
                decimal takeProfitPrice = entryPrice * (1 + settings.TakeProfit);

                // Open is known before the intrabar range; a gap therefore has deterministic priority.
                if (candle.OpenPrice <= stopPrice)
                {
                    ClosePosition(candle.OpenPrice, candle.OpenTime, "StopLoss");
                }
                else if (candle.OpenPrice >= takeProfitPrice)
                {
                    ClosePosition(candle.OpenPrice, candle.OpenTime, "TakeProfit");
                }
                // OHLC has no intrabar ordering, so simultaneous SL/TP is resolved conservatively.
                else if (candle.LowPrice <= stopPrice)
                {
                    ClosePosition(stopPrice, candle.OpenTime, "StopLoss");
                }
                else if (candle.HighPrice >= takeProfitPrice)
                {
                    ClosePosition(takeProfitPrice, candle.OpenTime, "TakeProfit");
                }
            }

            bool hasNextCandle = index + 1 < candles.Count;
            bool hasSignals = candle.OscillatorActions.Length == settings.OscillatorWeights.Length &&
                              candle.MovingAverageActions.Length == settings.MovingAverageWeights.Length;

            if (hasNextCandle && hasSignals)
            {
                decimal generalIndicator = CalculateGeneralIndicator(candle, settings);

                if (cryptoAmount == 0m && generalIndicator > settings.BuyThreshold)
                    pendingOrder = PendingOrder.Enter;
                else if (cryptoAmount > 0m && generalIndicator < settings.SellThreshold)
                    pendingOrder = PendingOrder.Exit;
            }

            bool isLastCandle = index == candles.Count - 1;
            if (isLastCandle && cryptoAmount > 0m)
                ClosePosition(candle.ClosePrice, candle.OpenTime, "End of Test");

            decimal equity = cryptoAmount > 0m
                ? cryptoAmount * candle.ClosePrice * (1 - settings.TradingFee)
                : cashBalance;

            dailyClosingEquity[candle.OpenTime.Date] = equity;
            if (equity > peakEquity)
                peakEquity = equity;

            decimal drawdown = peakEquity == 0m
                ? 0m
                : (peakEquity - equity) / peakEquity * 100m;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }

        result.FinalBalance = cashBalance;
        result.TotalProfit = cashBalance - initialDeposit;
        result.ProfitPercentage = result.TotalProfit / initialDeposit * 100m;
        result.TotalTrades = totalTrades;
        result.WinningTrades = winningTrades;
        result.LosingTrades = losingTrades;
        result.MaxDrawdown = maxDrawdown;
        if (grossLoss > 0m)
            result.ProfitFactor = grossProfit / grossLoss;
        else
            result.ProfitFactor = grossProfit > 0m ? decimal.MaxValue : 0m;
        result.SharpeRatio = CalculateAnnualizedSharpe(dailyClosingEquity.Values, initialDeposit);

        return result;
    }

    /// <summary>
    /// Compounds net trade returns instead of adding percentages arithmetically.
    /// </summary>
    internal static decimal CalculateCompoundedReturn(IEnumerable<TradeRecord> trades)
    {
        decimal equityMultiplier = 1m;
        foreach (var trade in trades.OrderBy(trade => trade.ExitTime))
            equityMultiplier *= 1m + trade.ProfitPercent / 100m;

        return (equityMultiplier - 1m) * 100m;
    }

    /// <summary>
    /// Calculates drawdown from a compounded trade-equity curve.
    /// </summary>
    internal static decimal CalculateTradeMaxDrawdown(IEnumerable<TradeRecord> trades)
    {
        decimal equity = 100m;
        decimal peak = equity;
        decimal maxDrawdown = 0m;

        foreach (var trade in trades.OrderBy(trade => trade.ExitTime))
        {
            equity *= 1m + trade.ProfitPercent / 100m;
            if (equity > peak)
                peak = equity;

            decimal drawdown = peak == 0m ? 0m : (peak - equity) / peak * 100m;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    /// <summary>
    /// Calculates a zero-risk-free-rate Sharpe ratio from daily equity observations for a 365-day market.
    /// </summary>
    internal static decimal CalculateAnnualizedSharpe(IEnumerable<decimal> dailyEquities, decimal initialEquity)
    {
        var returns = new List<double>();
        decimal previousEquity = initialEquity;

        foreach (decimal equity in dailyEquities)
        {
            if (previousEquity > 0m)
                returns.Add((double)(equity / previousEquity - 1m));
            previousEquity = equity;
        }

        if (returns.Count < 2)
            return 0m;

        double mean = returns.Average();
        double variance = returns.Sum(value => Math.Pow(value - mean, 2)) / (returns.Count - 1);
        double standardDeviation = Math.Sqrt(variance);

        return standardDeviation <= double.Epsilon
            ? 0m
            : (decimal)(mean / standardDeviation * Math.Sqrt(365d));
    }

    private static decimal CalculateGeneralIndicator(
        BacktestCandleSnapshot candle,
        BacktestSettings settings)
    {
        decimal oscillatorIndicator = CalculateWeightedIndicator(
            candle.OscillatorActions,
            settings.OscillatorWeights);
        decimal movingAverageIndicator = CalculateWeightedIndicator(
            candle.MovingAverageActions,
            settings.MovingAverageWeights);

        return oscillatorIndicator * settings.OscillatorGroupWeight +
               movingAverageIndicator * settings.MovingAverageGroupWeight;
    }

    private static decimal CalculateWeightedIndicator(decimal[] actions, decimal[] weights)
    {
        decimal weightedSum = 0m;
        decimal totalWeight = 0m;

        for (int index = 0; index < actions.Length; index++)
        {
            weightedSum += actions[index] * weights[index];
            totalWeight += weights[index];
        }

        return totalWeight == 0m ? 0m : weightedSum / totalWeight * 100m;
    }

    private static void ValidateInputs(
        IReadOnlyList<BacktestCandleSnapshot> candles,
        int simulationStartIndex,
        decimal initialDeposit,
        BacktestSettings settings)
    {
        if (candles == null || candles.Count == 0)
            throw new ArgumentException("Backtest candles are required.", nameof(candles));
        if (simulationStartIndex < 0 || simulationStartIndex >= candles.Count)
            throw new ArgumentOutOfRangeException(nameof(simulationStartIndex));
        if (initialDeposit <= 0m)
            throw new ArgumentOutOfRangeException(nameof(initialDeposit), "Initial deposit must be positive.");
        if (settings.OscillatorWeights == null || settings.OscillatorWeights.Length == 0)
            throw new ArgumentException("Oscillator weights are required.", nameof(settings));
        if (settings.MovingAverageWeights == null || settings.MovingAverageWeights.Length == 0)
            throw new ArgumentException("Moving-average weights are required.", nameof(settings));
        if (settings.StopLoss <= 0m || settings.StopLoss >= 1m)
            throw new ArgumentOutOfRangeException(nameof(settings), "Stop loss must be between zero and one.");
        if (settings.TakeProfit <= 0m)
            throw new ArgumentOutOfRangeException(nameof(settings), "Take profit must be positive.");
        if (settings.TradingFee < 0m || settings.TradingFee >= 1m)
            throw new ArgumentOutOfRangeException(nameof(settings), "Trading fee must be between zero and one.");
    }
}
