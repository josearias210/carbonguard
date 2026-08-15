using System.Diagnostics.Metrics;
using CarbonGuard.Core.Models;

namespace CarbonGuard.Api.Infrastructure;

public sealed partial class AnalysisTelemetry
{
    private readonly Histogram<double> _duration;
    private readonly ILogger<AnalysisTelemetry> _logger;
    private readonly Counter<long> _recordsAnalyzed;
    private readonly Counter<long> _recordsRequiringReview;

    public AnalysisTelemetry(ILogger<AnalysisTelemetry> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        Meter meter = meterFactory.Create("CarbonGuard.Api");
        _recordsAnalyzed = meter.CreateCounter<long>("carbonguard.analysis.records", "{record}");
        _recordsRequiringReview = meter.CreateCounter<long>("carbonguard.analysis.review_records", "{record}");
        _duration = meter.CreateHistogram<double>("carbonguard.analysis.duration", "ms");
    }

    public void Record(AnalysisResponse response, TimeSpan elapsed, string traceId)
    {
        var policyVersion = new KeyValuePair<string, object?>("policy.version", response.Policy.Version);
        _recordsAnalyzed.Add(response.Summary.TotalRecords, policyVersion);
        _recordsRequiringReview.Add(response.Summary.RecordsRequiringReview, policyVersion);
        _duration.Record(elapsed.TotalMilliseconds, policyVersion);

        LogAnalysisCompleted(
            _logger,
            traceId,
            response.Policy.Version,
            response.Summary.TotalRecords,
            response.Summary.RecordsRequiringReview,
            response.Summary.PartiallyEvaluated,
            elapsed.TotalMilliseconds);
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Analysis {TraceId} completed with policy {PolicyVersion}: {TotalRecords} records, {ReviewRecords} requiring review, {PartiallyEvaluated} partially evaluated, in {ElapsedMilliseconds:F2} ms")]
    private static partial void LogAnalysisCompleted(
        ILogger logger,
        string traceId,
        string policyVersion,
        int totalRecords,
        int reviewRecords,
        int partiallyEvaluated,
        double elapsedMilliseconds);
}

