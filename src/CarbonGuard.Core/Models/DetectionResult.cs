namespace CarbonGuard.Core.Models;

public sealed record DetectionResult(
    long Id,
    string? Site,
    string? Month,
    bool RequiresReview,
    AssessmentStatus Status,
    Severity Severity,
    IReadOnlyList<AnomalyFinding> Findings,
    IReadOnlyList<RuleEvaluation> Evaluations);
