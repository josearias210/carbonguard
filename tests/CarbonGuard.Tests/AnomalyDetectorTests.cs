using CarbonGuard.Core.Configuration;
using CarbonGuard.Core.Detection;
using CarbonGuard.Core.Models;

namespace CarbonGuard.Tests;

public sealed class AnomalyDetectorTests
{
    private readonly AnomalyDetector _detector = new(new AnomalyDetectionOptions());

    [Fact]
    public void Analyze_FlagsExpectedRecordsFromChallengeDataset()
    {
        AnalysisResponse response = _detector.Analyze(new AnalysisRequest(ChallengeRecords));

        long[] reviewedIds = response.Results
            .Where(result => result.RequiresReview)
            .Select(result => result.Id)
            .ToArray();

        Assert.Equal([4L, 7L, 8L], reviewedIds);
        Assert.Contains(response.Results.Single(result => result.Id == 4).Findings,
            finding => finding.Code == FindingCodes.EnergyBehaviorOutlier);
        Assert.Contains(response.Results.Single(result => result.Id == 7).Findings,
            finding => finding.Code == FindingCodes.InvalidEnergy);
        Assert.Contains(response.Results.Single(result => result.Id == 8).Findings,
            finding => finding.Code == FindingCodes.SuspiciousEmissionIntensity);
        Assert.Contains(response.Results.Single(result => result.Id == 8).Evaluations,
            evaluation => evaluation is
            {
                Rule: "EmissionIntensity",
                Outcome: RuleOutcome.Flagged,
                BaselineScope: "global-fallback",
            });
        Assert.Equal(3, response.Summary.RecordsRequiringReview);
        Assert.Equal("2026-08", response.Policy.Version);
    }

    [Fact]
    public void Analyze_ApprovedExpansionExplainsEnergyChangeWithoutHidingAuditTrail()
    {
        EmissionRecord[] records =
        [
            new(1, "Madrid", "2026-01", 12_000, 2_800),
            new(2, "Madrid", "2026-02", 12_500, 2_900),
            new(3, "Madrid", "2026-03", 12_800, 2_950),
            new(4, "Madrid", "2026-05", 25_000, 5_900),
        ];
        BusinessEvent[] events =
        [
            new("Madrid", "2026-05", "2026-06", "CapacityExpansion", 1.8m, 2.2m, true, true,
                "A new production line was commissioned."),
        ];

        DetectionResult result = _detector.Analyze(new AnalysisRequest(records, events)).Results.Single(record => record.Id == 4);

        Assert.False(result.RequiresReview);
        Assert.Equal(Severity.Info, result.Severity);
        Assert.Contains(result.Findings, finding => finding.Code == FindingCodes.ExplainedByApprovedContext);
        Assert.Equal(AssessmentStatus.ExplainedByContext, result.Status);
    }

    [Fact]
    public void Analyze_UnapprovedExpansionStillRequiresReview()
    {
        EmissionRecord[] records =
        [
            new(1, "Madrid", "2026-01", 12_000, 2_800),
            new(2, "Madrid", "2026-02", 12_500, 2_900),
            new(3, "Madrid", "2026-03", 12_800, 2_950),
            new(4, "Madrid", "2026-05", 25_000, 5_900),
        ];
        BusinessEvent[] events =
        [
            new("Madrid", "2026-05", "2026-06", "CapacityExpansion", 1.8m, 2.2m, false, true,
                "Unverified customer note."),
        ];

        DetectionResult result = _detector.Analyze(new AnalysisRequest(records, events)).Results.Single(record => record.Id == 4);

        Assert.True(result.RequiresReview);
        Assert.Contains(result.Findings, finding => finding.Code == FindingCodes.EnergyBehaviorOutlier);
    }

    [Fact]
    public void Analyze_DoesNotFlagBehaviorWithoutEnoughHistory()
    {
        EmissionRecord[] records =
        [
            new(1, "New site", "2026-01", 100, 20),
            new(2, "New site", "2026-02", 10_000, 2_000),
        ];

        AnalysisResponse response = _detector.Analyze(new AnalysisRequest(records));

        Assert.All(response.Results, result => Assert.False(result.RequiresReview));
        Assert.All(response.Results, result => Assert.Equal(AssessmentStatus.PartiallyEvaluated, result.Status));
        Assert.Equal(2, response.Summary.PartiallyEvaluated);
    }

    [Fact]
    public void Analyze_RejectsDuplicateSiteAndMonth()
    {
        EmissionRecord[] records =
        [
            new(1, "Madrid", "2026-01", 12_000, 2_800),
            new(2, " madrid ", "2026-01", 12_100, 2_810),
        ];

        AnalysisResponse response = _detector.Analyze(new AnalysisRequest(records));

        Assert.All(response.Results, result =>
        {
            Assert.True(result.RequiresReview);
            Assert.Contains(result.Findings, finding => finding.Code == FindingCodes.DuplicateSiteMonth);
        });
    }

    [Fact]
    public void Analyze_PrefersSiteIntensityBaselineOverDifferentGlobalCohort()
    {
        EmissionRecord[] records =
        [
            new(1, "Factory", "2026-01", 1_000, 200),
            new(2, "Factory", "2026-02", 1_000, 205),
            new(3, "Factory", "2026-03", 1_000, 195),
            new(4, "Office", "2026-01", 1_000, 1_000),
            new(5, "Office", "2026-02", 1_000, 1_020),
            new(6, "Office", "2026-03", 1_000, 980),
            new(7, "Factory", "2026-04", 1_000, 202),
        ];

        DetectionResult result = _detector.Analyze(new AnalysisRequest(records)).Results.Single(record => record.Id == 7);

        Assert.False(result.RequiresReview);
        Assert.Contains(result.Evaluations, evaluation => evaluation is
        {
            Rule: "EmissionIntensity",
            Outcome: RuleOutcome.Passed,
            BaselineScope: "site",
            HistoricalSampleSize: 3,
        });
    }

    [Fact]
    public void Analyze_ApprovedEnergyContextDoesNotSuppressIntensityAnomaly()
    {
        EmissionRecord[] records =
        [
            new(1, "Madrid", "2026-01", 12_000, 2_800),
            new(2, "Madrid", "2026-02", 12_500, 2_900),
            new(3, "Madrid", "2026-03", 12_800, 2_950),
            new(4, "Madrid", "2026-05", 25_000, 50_000),
        ];
        BusinessEvent[] events =
        [
            new("Madrid", "2026-05", "2026-06", "CapacityExpansion", 1.8m, 2.2m, true, true,
                "A new production line was commissioned."),
        ];

        DetectionResult result = _detector.Analyze(new AnalysisRequest(records, events)).Results.Single(record => record.Id == 4);

        Assert.True(result.RequiresReview);
        Assert.Equal(AssessmentStatus.RequiresReview, result.Status);
        Assert.Contains(result.Findings, finding => finding.Code == FindingCodes.ExplainedByApprovedContext);
        Assert.Contains(result.Findings, finding => finding.Code == FindingCodes.SuspiciousEmissionIntensity);
    }

    [Fact]
    public void Analyze_IsIndependentOfInputOrderWithinTheSamePeriod()
    {
        EmissionRecord[] reversed = ChallengeRecords.Reverse().ToArray();

        AnalysisResponse original = _detector.Analyze(new AnalysisRequest(ChallengeRecords));
        AnalysisResponse reordered = _detector.Analyze(new AnalysisRequest(reversed));

        long[] originalReviewed = original.Results.Where(result => result.RequiresReview).Select(result => result.Id).Order().ToArray();
        long[] reorderedReviewed = reordered.Results.Where(result => result.RequiresReview).Select(result => result.Id).Order().ToArray();
        Assert.Equal(originalReviewed, reorderedReviewed);
    }

    [Fact]
    public void Analyze_StructuralEventStartsANewEnergyBaselineSegment()
    {
        EmissionRecord[] records =
        [
            new(1, "Madrid", "2026-01", 12_000, 2_800),
            new(2, "Madrid", "2026-02", 12_500, 2_900),
            new(3, "Madrid", "2026-03", 12_800, 2_950),
            new(4, "Madrid", "2026-05", 25_000, 5_900),
            new(5, "Madrid", "2026-06", 26_000, 6_100),
            new(6, "Madrid", "2026-07", 27_000, 6_300),
            new(7, "Madrid", "2026-08", 26_500, 6_200),
        ];
        BusinessEvent[] events =
        [
            new("Madrid", "2026-05", "2026-05", "CapacityExpansion", 1.8m, 2.2m, true, true,
                "A new production line creates a new operating regime."),
        ];

        AnalysisResponse response = _detector.Analyze(new AnalysisRequest(records, events));
        DetectionResult august = response.Results.Single(record => record.Id == 7);

        Assert.False(august.RequiresReview);
        Assert.Contains(august.Evaluations, evaluation => evaluation is
        {
            Rule: "EnergyBehavior",
            Outcome: RuleOutcome.Passed,
            HistoricalSampleSize: 3,
        });
    }

    private static readonly EmissionRecord[] ChallengeRecords =
    [
        new(1, "Madrid", "2026-01", 12_000, 2_800),
        new(2, "Madrid", "2026-02", 12_500, 2_900),
        new(3, "Madrid", "2026-03", 12_800, 2_950),
        new(4, "Madrid", "2026-04", 79_000, 18_200),
        new(5, "Barcelona", "2026-01", 8_500, 1_950),
        new(6, "Barcelona", "2026-02", 8_700, 2_000),
        new(7, "Barcelona", "2026-03", -900, -210),
        new(8, "Barcelona", "2026-04", 8_900, 8_500),
        new(9, "Valencia", "2026-01", 6_200, 1_450),
        new(10, "Valencia", "2026-02", 6_250, 1_460),
    ];
}
