using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using TechnicalAnalysis.Domain.Models;
using TechnicalAnalysis.Shared;

namespace TechnicalAnalysis.Domain.Services;

public sealed class TournamentSignalService
{
    private const int RequiredCandles = 120;
    private const decimal MaximumRiskPercent = 0.5m;
    private static readonly IReadOnlyDictionary<KlineInterval, int> IntervalMinutes =
        new Dictionary<KlineInterval, int>
        {
            [KlineInterval.OneMinute] = 1,
            [KlineInterval.FiveMinutes] = 5,
            [KlineInterval.FifteenMinutes] = 15,
            [KlineInterval.OneHour] = 60
        };

    private readonly BinanceService _binanceService;

    public TournamentSignalService(BinanceService binanceService)
    {
        _binanceService = binanceService;
    }

    public async Task<TournamentSignalResult> GetSignalAsync(string asset)
    {
        CurrencyPair pair = ParseSupportedAsset(asset);
        DateTime generatedAt = DateTime.UtcNow;
        var intervals = IntervalMinutes.Keys.ToArray();
        var requests = intervals.Select(async interval =>
        {
            var candles = await _binanceService.GetRecentClosedKlinesAsync(pair, interval, 200);
            return (Interval: interval, Candles: (IReadOnlyList<BinanceSpotKline>)candles);
        });
        var data = (await Task.WhenAll(requests)).ToDictionary(item => item.Interval, item => item.Candles);
        return BuildSignal(pair.ToString(), generatedAt, data);
    }

    public async Task<IReadOnlyList<TournamentSignalResult>> GetSignalsAsync() =>
        await Task.WhenAll(GetSignalAsync("ETHUSDT"), GetSignalAsync("SOLUSDT"));

    internal static CurrencyPair ParseSupportedAsset(string asset)
    {
        if (!Enum.TryParse(asset, ignoreCase: true, out CurrencyPair pair) ||
            pair is not CurrencyPair.ETHUSDT and not CurrencyPair.SOLUSDT)
        {
            throw new ArgumentException("Tournament signal supports only ETHUSDT and SOLUSDT.", nameof(asset));
        }

        return pair;
    }

    internal static TournamentSignalResult BuildSignal(
        string asset,
        DateTime generatedAt,
        IReadOnlyDictionary<KlineInterval, IReadOnlyList<BinanceSpotKline>> candlesByInterval)
    {
        var snapshots = IntervalMinutes.Keys
            .Select(interval => CreateSnapshot(interval, GetCandles(candlesByInterval, interval)))
            .ToDictionary(snapshot => snapshot.Timeframe);
        TournamentTimeframeSnapshot oneMinute = snapshots[KlineInterval.OneMinute.ToString()];
        TournamentTimeframeSnapshot fiveMinutes = snapshots[KlineInterval.FiveMinutes.ToString()];
        TournamentTimeframeSnapshot fifteenMinutes = snapshots[KlineInterval.FifteenMinutes.ToString()];
        TournamentTimeframeSnapshot oneHour = snapshots[KlineInterval.OneHour.ToString()];
        int weightedScore = (int)Math.Round(
            oneHour.Score * 0.35m +
            fifteenMinutes.Score * 0.30m +
            fiveMinutes.Score * 0.25m +
            oneMinute.Score * 0.10m);
        bool bullishAlignment = oneHour.Score >= 25 && fifteenMinutes.Score >= 25 &&
                                fiveMinutes.Score >= 25 && oneMinute.Score > -25;
        bool bearishAlignment = oneHour.Score <= -25 && fifteenMinutes.Score <= -25 &&
                                fiveMinutes.Score <= -25 && oneMinute.Score < 25;
        bool buy = bullishAlignment && weightedScore >= 30 && fiveMinutes.Rsi14 is >= 48m and <= 75m;
        bool sell = bearishAlignment && weightedScore <= -30;
        string action = "NO_TRADE";
        if (buy) action = "BUY";
        else if (sell) action = "SELL";
        int volumeConfidenceBonus = fiveMinutes.VolumeRatio >= 1.2m ? 5 : 0;
        int confidence = action == "NO_TRADE"
            ? Math.Clamp(Math.Abs(weightedScore), 0, 60)
            : Math.Clamp(55 + Math.Abs(weightedScore) / 2 +
                         volumeConfidenceBonus, 55, 95);
        decimal referencePrice = fiveMinutes.LastPrice;
        decimal atr = fiveMinutes.Atr14;
        bool fastBreakout = buy && fiveMinutes.BrokeRecentHigh && fiveMinutes.VolumeRatio >= 1.4m;
        if (sell)
            fastBreakout = fiveMinutes.BrokeRecentLow && fiveMinutes.VolumeRatio >= 1.4m;
        string orderType = null;
        if (action != "NO_TRADE")
            orderType = fastBreakout && confidence >= 75 ? "MARKET" : "LIMIT";
        decimal? limitPrice = null;
        decimal? stopLoss = null;
        decimal? takeProfit = null;

        if (buy)
        {
            decimal entry = orderType == "MARKET" ? referencePrice : referencePrice - atr * 0.15m;
            decimal stopDistance = Math.Max(atr * 1.2m, entry * 0.003m);
            limitPrice = orderType == "LIMIT" ? RoundPrice(entry) : null;
            stopLoss = RoundPrice(entry - stopDistance);
            takeProfit = RoundPrice(entry + stopDistance * 2m);
        }
        else if (sell)
        {
            limitPrice = orderType == "LIMIT" ? RoundPrice(referencePrice + atr * 0.1m) : null;
        }

        DateTime dataAsOf = snapshots.Values.Max(snapshot => snapshot.LastClosedCandleTime);
        bool stale = generatedAt - oneMinute.LastClosedCandleTime > TimeSpan.FromMinutes(3) ||
                     generatedAt - fiveMinutes.LastClosedCandleTime > TimeSpan.FromMinutes(10);
        var reasons = BuildReasons(
            action,
            weightedScore,
            oneHour,
            fifteenMinutes,
            fiveMinutes,
            oneMinute,
            stale);
        if (stale)
        {
            action = "NO_TRADE";
            orderType = null;
            limitPrice = null;
            stopLoss = null;
            takeProfit = null;
            confidence = 0;
        }

        return new TournamentSignalResult
        {
            Asset = asset,
            Action = action,
            Confidence = confidence,
            SuggestedOrderType = orderType,
            ReferencePrice = RoundPrice(referencePrice),
            LimitPrice = limitPrice,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            RiskRewardRatio = action == "BUY" ? 2m : null,
            MaximumAccountRiskPercent = MaximumRiskPercent,
            PositionSizeFormula = action == "BUY"
                ? "quantity = accountBalance * 0.005 / abs(entryPrice - stopLoss)"
                : null,
            GeneratedAt = generatedAt,
            DataAsOf = dataAsOf,
            ExpiresAt = generatedAt.AddMinutes(5),
            IsStale = stale,
            CanOpenNewSpotPosition = action == "BUY",
            Warning = action == "SELL"
                ? "Spot SELL is an exit signal for an existing position; it is not permission to open a short."
                : "Experimental tournament signal, not a profit guarantee. Do not average down or exceed the risk cap.",
            Reasons = reasons,
            Timeframes = snapshots.Values
                .OrderBy(snapshot => IntervalMinutes[Enum.Parse<KlineInterval>(snapshot.Timeframe)])
                .ToList()
        };
    }

    private static IReadOnlyList<BinanceSpotKline> GetCandles(
        IReadOnlyDictionary<KlineInterval, IReadOnlyList<BinanceSpotKline>> source,
        KlineInterval interval)
    {
        if (!source.TryGetValue(interval, out IReadOnlyList<BinanceSpotKline> candles) ||
            candles.Count < RequiredCandles)
        {
            throw new InvalidOperationException(
                $"At least {RequiredCandles} closed {interval} candles are required for a tournament signal.");
        }

        return candles.OrderBy(candle => candle.OpenTime).ToList();
    }

    private static TournamentTimeframeSnapshot CreateSnapshot(
        KlineInterval interval,
        IReadOnlyList<BinanceSpotKline> candles)
    {
        decimal[] closes = candles.Select(candle => candle.ClosePrice).ToArray();
        decimal[] ema20 = Ema(closes, 20);
        decimal[] ema50 = Ema(closes, 50);
        decimal rsi = Rsi(closes, 14);
        decimal atr = Atr(candles, 14);
        BinanceSpotKline last = candles[^1];
        decimal medianVolume = Median(candles.Skip(candles.Count - 21).Take(20).Select(candle => candle.Volume));
        decimal volumeRatio = medianVolume <= 0m ? 0m : last.Volume / medianVolume;
        decimal priorHigh = candles.Skip(candles.Count - 21).Take(20).Max(candle => candle.HighPrice);
        decimal priorLow = candles.Skip(candles.Count - 21).Take(20).Min(candle => candle.LowPrice);
        bool brokeHigh = last.ClosePrice > priorHigh;
        bool brokeLow = last.ClosePrice < priorLow;
        int score = 0;
        score += last.ClosePrice >= ema20[^1] ? 15 : -15;
        score += ema20[^1] >= ema50[^1] ? 25 : -25;
        score += ema20[^1] >= ema20[^6] ? 15 : -15;
        if (rsi is >= 52m and <= 75m) score += 15;
        else if (rsi is >= 25m and <= 48m) score -= 15;
        else if (rsi > 80m) score -= 10;
        else if (rsi < 20m) score += 10;
        if (brokeHigh) score += volumeRatio >= 1.2m ? 20 : 8;
        if (brokeLow) score -= volumeRatio >= 1.2m ? 20 : 8;
        score = Math.Clamp(score, -100, 100);

        return new TournamentTimeframeSnapshot
        {
            Timeframe = interval.ToString(),
            LastClosedCandleTime = last.CloseTime,
            LastPrice = RoundPrice(last.ClosePrice),
            Ema20 = RoundPrice(ema20[^1]),
            Ema50 = RoundPrice(ema50[^1]),
            Rsi14 = Math.Round(rsi, 2),
            Atr14 = RoundPrice(atr),
            AtrPercent = last.ClosePrice <= 0m ? 0m : Math.Round(atr / last.ClosePrice * 100m, 3),
            VolumeRatio = Math.Round(volumeRatio, 2),
            BrokeRecentHigh = brokeHigh,
            BrokeRecentLow = brokeLow,
            Score = score,
            Trend = GetTrend(score)
        };
    }

    private static string GetTrend(int score)
    {
        if (score >= 25) return "BULLISH";
        return score <= -25 ? "BEARISH" : "NEUTRAL";
    }

    private static List<string> BuildReasons(
        string action,
        int weightedScore,
        TournamentTimeframeSnapshot oneHour,
        TournamentTimeframeSnapshot fifteenMinutes,
        TournamentTimeframeSnapshot fiveMinutes,
        TournamentTimeframeSnapshot oneMinute,
        bool stale)
    {
        var reasons = new List<string>
        {
            $"Weighted multi-timeframe score: {weightedScore}.",
            $"1h/15m trend: {oneHour.Trend}/{fifteenMinutes.Trend}.",
            $"5m RSI {fiveMinutes.Rsi14:0.##}, volume {fiveMinutes.VolumeRatio:0.##}x median.",
            $"1m execution trend: {oneMinute.Trend}."
        };
        if (action == "NO_TRADE")
            reasons.Add("Trend, entry and execution timeframes are not sufficiently aligned.");
        if (fiveMinutes.Rsi14 > 75m)
            reasons.Add("5m RSI is overextended; do not chase the move.");
        if (stale)
            reasons.Add("Market data is stale; trading is blocked.");
        return reasons;
    }

    private static decimal[] Ema(IReadOnlyList<decimal> values, int period)
    {
        var result = new decimal[values.Count];
        decimal multiplier = 2m / (period + 1m);
        result[0] = values[0];
        for (int index = 1; index < values.Count; index++)
            result[index] = (values[index] - result[index - 1]) * multiplier + result[index - 1];
        return result;
    }

    private static decimal Rsi(IReadOnlyList<decimal> closes, int period)
    {
        decimal gain = 0m;
        decimal loss = 0m;
        for (int index = 1; index <= period; index++)
        {
            decimal change = closes[index] - closes[index - 1];
            gain += Math.Max(0m, change);
            loss += Math.Max(0m, -change);
        }

        gain /= period;
        loss /= period;
        for (int index = period + 1; index < closes.Count; index++)
        {
            decimal change = closes[index] - closes[index - 1];
            gain = (gain * (period - 1) + Math.Max(0m, change)) / period;
            loss = (loss * (period - 1) + Math.Max(0m, -change)) / period;
        }

        if (loss == 0m) return gain == 0m ? 50m : 100m;
        decimal relativeStrength = gain / loss;
        return 100m - 100m / (1m + relativeStrength);
    }

    private static decimal Atr(IReadOnlyList<BinanceSpotKline> candles, int period)
    {
        var ranges = new decimal[candles.Count - 1];
        for (int index = 1; index < candles.Count; index++)
        {
            BinanceSpotKline candle = candles[index];
            decimal previousClose = candles[index - 1].ClosePrice;
            ranges[index - 1] = Math.Max(
                candle.HighPrice - candle.LowPrice,
                Math.Max(Math.Abs(candle.HighPrice - previousClose), Math.Abs(candle.LowPrice - previousClose)));
        }

        decimal atr = ranges.Take(period).Average();
        for (int index = period; index < ranges.Length; index++)
            atr = (atr * (period - 1) + ranges[index]) / period;
        return atr;
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        decimal[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0) return 0m;
        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2m;
    }

    private static decimal RoundPrice(decimal value) => Math.Round(value, 2);
}
