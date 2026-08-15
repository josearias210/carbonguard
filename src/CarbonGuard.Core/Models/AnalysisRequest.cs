namespace CarbonGuard.Core.Models;

public sealed record AnalysisRequest(
    IReadOnlyList<EmissionRecord>? Records,
    IReadOnlyList<BusinessEvent>? BusinessEvents = null);

