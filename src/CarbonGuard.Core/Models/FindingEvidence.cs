namespace CarbonGuard.Core.Models;

public sealed record FindingEvidence(
    string Metric,
    decimal? Observed = null,
    decimal? BaselineMedian = null,
    decimal? RobustZScore = null,
    string? ExpectedRange = null);

