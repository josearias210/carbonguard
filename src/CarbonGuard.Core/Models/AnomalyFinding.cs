namespace CarbonGuard.Core.Models;

public sealed record AnomalyFinding(
    string Code,
    string Rule,
    string Reason,
    Severity Severity,
    FindingEvidence Evidence);

