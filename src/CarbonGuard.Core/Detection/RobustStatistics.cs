namespace CarbonGuard.Core.Detection;

internal static class RobustStatistics
{
    private const decimal NormalConsistencyFactor = 0.6745m;

    public static RobustScore Calculate(IReadOnlyCollection<decimal> samples, decimal observed)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one sample is required.", nameof(samples));
        }

        decimal median = Median(samples);
        decimal medianAbsoluteDeviation = Median(samples.Select(value => Math.Abs(value - median)).ToArray());
        decimal relativeDeviation = median == 0m
            ? decimal.MaxValue
            : Math.Abs(observed - median) / Math.Abs(median);
        decimal? robustZScore = medianAbsoluteDeviation == 0m
            ? null
            : NormalConsistencyFactor * Math.Abs(observed - median) / medianAbsoluteDeviation;

        return new RobustScore(median, medianAbsoluteDeviation, relativeDeviation, robustZScore);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        decimal[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;

        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2m
            : sorted[middle];
    }
}

internal sealed record RobustScore(
    decimal Median,
    decimal MedianAbsoluteDeviation,
    decimal RelativeDeviation,
    decimal? RobustZScore)
{
    public bool IsAnomalous(decimal zScoreThreshold, decimal relativeDeviationThreshold) =>
        RelativeDeviation >= relativeDeviationThreshold &&
        (MedianAbsoluteDeviation == 0m || RobustZScore >= zScoreThreshold);
}
