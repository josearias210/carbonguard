namespace CarbonGuard.Core.Models;

public sealed record AnalysisResponse(
    string EngineVersion,
    DetectionPolicy Policy,
    AnalysisSummary Summary,
    IReadOnlyList<DetectionResult> Results);
