namespace CarbonGuard.Core.Models;

public sealed record AnalysisSummary(
    int TotalRecords,
    int RecordsRequiringReview,
    int PartiallyEvaluated,
    int ExplainedByContext,
    int Critical,
    int High,
    int Medium);
