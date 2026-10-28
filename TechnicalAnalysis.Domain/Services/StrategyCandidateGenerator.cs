using TechnicalAnalysis.Domain.MLModels;

namespace TechnicalAnalysis.Domain.Services;

internal static class StrategyCandidateGenerator
{
    internal const string CandidateSetVersion = "wf-v1-sparse-4x4";

    internal static IReadOnlyList<OptimizationInput> Generate(
        string asset,
        string timeframe,
        int count,
        string seedScope)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var random = new Random(StableSeed($"{CandidateSetVersion}|{asset}|{timeframe}|{seedScope}"));
        var candidates = new List<OptimizationInput>(count);
        for (int index = 0; index < count; index++)
            candidates.Add(GenerateOne(random, asset, timeframe));
        return candidates;
    }

    private static OptimizationInput GenerateOne(Random random, string asset, string timeframe)
    {
        float[] oscillators = GenerateSparseWeights(random, 11, 4);
        float[] movingAverages = GenerateSparseWeights(random, 13, 4);
        float oscillatorGroupWeight = 0.25f + (float)random.NextDouble() * 0.5f;

        return new OptimizationInput
        {
            Asset = asset,
            Timeframe = timeframe,
            RsiWeight = oscillators[0],
            StochasticKWeight = oscillators[1],
            CciWeight = oscillators[2],
            AdxWeight = oscillators[3],
            AoWeight = oscillators[4],
            MomentumWeight = oscillators[5],
            MacdWeight = oscillators[6],
            UltimateWeight = oscillators[7],
            WilliamsRWeight = oscillators[8],
            BullBearWeight = oscillators[9],
            StochasticRsiWeight = oscillators[10],
            Sma10Weight = movingAverages[0],
            Ema10Weight = movingAverages[1],
            Sma20Weight = movingAverages[2],
            Ema20Weight = movingAverages[3],
            Sma30Weight = movingAverages[4],
            Ema30Weight = movingAverages[5],
            Sma50Weight = movingAverages[6],
            Ema50Weight = movingAverages[7],
            Sma100Weight = movingAverages[8],
            Ema100Weight = movingAverages[9],
            Sma200Weight = movingAverages[10],
            Ema200Weight = movingAverages[11],
            Hma9Weight = movingAverages[12],
            BuyThreshold = random.Next(20, 60),
            SellThreshold = random.Next(-60, -20),
            StopLoss = 0.01f + (float)random.NextDouble() * 0.04f,
            TakeProfit = 0.02f + (float)random.NextDouble() * 0.13f,
            OscillatorGroupWeight = oscillatorGroupWeight,
            MaGroupWeight = 1f - oscillatorGroupWeight
        };
    }

    private static float[] GenerateSparseWeights(Random random, int length, int activeCount)
    {
        var weights = new float[length];
        var indexes = Enumerable.Range(0, length).OrderBy(_ => random.Next()).Take(activeCount).ToArray();
        float sum = 0f;
        foreach (int index in indexes)
        {
            weights[index] = 0.1f + (float)random.NextDouble();
            sum += weights[index];
        }

        foreach (int index in indexes)
            weights[index] /= sum;
        return weights;
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }
}