namespace CarbonGuard.Core.Models;

public sealed record RuleEvaluation(
    string Rule,
    string Version,
    RuleOutcome Outcome,
    int HistoricalSampleSize,
    string? BaselineScope = null,
    string? Detail = null);

