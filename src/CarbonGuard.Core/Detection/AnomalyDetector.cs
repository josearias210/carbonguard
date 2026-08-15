using System.Globalization;
using CarbonGuard.Core.Configuration;
using CarbonGuard.Core.Models;

namespace CarbonGuard.Core.Detection;

public sealed class AnomalyDetector : IAnomalyDetector
{
    private const string EngineVersion = "2.0";
    private const string PolicyVersion = "2026-08";
    private const string DataQualityRule = "DataQuality";
    private const string EnergyBehaviorRule = "EnergyBehavior";
    private const string EmissionIntensityRule = "EmissionIntensity";
    private const string MonthFormat = "yyyy-MM";
    private readonly AnomalyDetectionOptions _options;

    public AnomalyDetector(AnomalyDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsValid())
        {
            throw new ArgumentException("Anomaly detection options are invalid.", nameof(options));
        }

        _options = options;
    }

    public AnalysisResponse Analyze(AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<EmissionRecord> records = request.Records ?? [];
        IReadOnlyList<BusinessEvent> businessEvents = request.BusinessEvents ?? [];
        PreparedRecord[] preparedRecords = Prepare(records);
        HashSet<long> duplicateIds = FindDuplicateIds(records);
        HashSet<string> duplicateSiteMonths = FindDuplicateSiteMonths(preparedRecords);
        var states = preparedRecords.ToDictionary(
            record => record.Index,
            record => CreateInitialState(record, duplicateIds, duplicateSiteMonths));

        var energyHistories = new Dictionary<string, RollingHistory>(StringComparer.Ordinal);
        var siteIntensityHistories = new Dictionary<string, RollingHistory>(StringComparer.Ordinal);
        var globalIntensityHistory = new RollingHistory(_options.GlobalIntensityHistoryWindow);

        foreach (IGrouping<DateOnly?, PreparedRecord> periodGroup in preparedRecords
                     .Where(record => record.Period is not null && states[record.Index].Findings.Count == 0)
                     .OrderBy(record => record.Period)
                     .ThenBy(record => record.Index)
                     .GroupBy(record => record.Period))
        {
            PreparedRecord[] currentPeriod = periodGroup.ToArray();

            foreach (PreparedRecord current in currentPeriod)
            {
                EvaluationState state = states[current.Index];
                RuleDecision energyDecision = EvaluateEnergyBehavior(
                    current,
                    GetOrCreateHistory(energyHistories, current.NormalizedSite, _options.EnergyHistoryWindow),
                    businessEvents);
                RuleDecision intensityDecision = EvaluateEmissionIntensity(
                    current,
                    GetOrCreateHistory(siteIntensityHistories, current.NormalizedSite, _options.SiteIntensityHistoryWindow),
                    globalIntensityHistory);

                state.Add(energyDecision);
                state.Add(intensityDecision);
            }

            // Update after evaluating the whole period so results do not depend on input order.
            foreach (PreparedRecord current in currentPeriod)
            {
                EvaluationState state = states[current.Index];
                if (state.CanLearnFrom(EnergyBehaviorRule))
                {
                    if (state.ShouldResetBaseline(EnergyBehaviorRule))
                    {
                        energyHistories[current.NormalizedSite].Clear();
                    }

                    energyHistories[current.NormalizedSite].Add(current.Source.EnergyKwh);
                }

                if (state.CanLearnFrom(EmissionIntensityRule))
                {
                    decimal intensity = current.Source.Co2Kg / current.Source.EnergyKwh;
                    siteIntensityHistories[current.NormalizedSite].Add(intensity);
                    globalIntensityHistory.Add(intensity);
                }
            }
        }

        DetectionResult[] results = preparedRecords
            .OrderBy(record => record.Index)
            .Select(record => states[record.Index].ToResult(record.Source))
            .ToArray();

        return new AnalysisResponse(EngineVersion, BuildPolicy(), BuildSummary(results), results);
    }

    private RuleDecision EvaluateEnergyBehavior(
        PreparedRecord current,
        RollingHistory history,
        IReadOnlyCollection<BusinessEvent> businessEvents)
    {
        if (history.Count < _options.MinimumEnergyHistory)
        {
            return RuleDecision.NotEvaluated(
                EnergyBehaviorRule,
                history.Count,
                "site",
                $"At least {_options.MinimumEnergyHistory} prior valid periods are required.");
        }

        decimal[] samples = history.Snapshot();
        RobustScore score = RobustStatistics.Calculate(samples, current.Source.EnergyKwh);
        if (!score.IsAnomalous(_options.RobustZScoreThreshold, _options.MinimumEnergyRelativeDeviation))
        {
            return RuleDecision.Passed(EnergyBehaviorRule, samples.Length, "site");
        }

        decimal multiplier = current.Source.EnergyKwh / score.Median;
        BusinessEvent? approvedContext = FindApprovedContext(current, multiplier, businessEvents);
        if (approvedContext is not null)
        {
            return RuleDecision.Explained(
                EnergyBehaviorRule,
                samples.Length,
                "site",
                approvedContext.ResetsBaseline,
                new AnomalyFinding(
                    FindingCodes.ExplainedByApprovedContext,
                    EnergyBehaviorRule,
                    $"Energy change is consistent with approved business event '{approvedContext.Type}'.",
                    Severity.Info,
                    new FindingEvidence(
                        "energyKwh",
                        current.Source.EnergyKwh,
                        score.Median,
                        score.RobustZScore,
                        $"{approvedContext.ExpectedEnergyMultiplierMin:0.##}x-{approvedContext.ExpectedEnergyMultiplierMax:0.##}x baseline")));
        }

        return RuleDecision.Flagged(
            EnergyBehaviorRule,
            samples.Length,
            "site",
            new AnomalyFinding(
                FindingCodes.EnergyBehaviorOutlier,
                EnergyBehaviorRule,
                "Energy consumption deviates significantly from the site's prior behavior.",
                Severity.High,
                new FindingEvidence(
                    "energyKwh",
                    current.Source.EnergyKwh,
                    score.Median,
                    score.RobustZScore,
                    $"maximum accepted relative deviation: {_options.MinimumEnergyRelativeDeviation:P0}")));
    }

    private RuleDecision EvaluateEmissionIntensity(
        PreparedRecord current,
        RollingHistory siteHistory,
        RollingHistory globalHistory)
    {
        RollingHistory? selectedHistory = siteHistory.Count >= _options.MinimumSiteIntensityHistory
            ? siteHistory
            : globalHistory.Count >= _options.MinimumGlobalIntensityHistory
                ? globalHistory
                : null;
        string? scope = selectedHistory == siteHistory ? "site" : selectedHistory is null ? null : "global-fallback";

        if (selectedHistory is null)
        {
            return RuleDecision.NotEvaluated(
                EmissionIntensityRule,
                Math.Max(siteHistory.Count, globalHistory.Count),
                null,
                "Neither the site nor global fallback has enough prior valid observations.");
        }

        decimal currentIntensity = current.Source.Co2Kg / current.Source.EnergyKwh;
        decimal[] samples = selectedHistory.Snapshot();
        RobustScore score = RobustStatistics.Calculate(samples, currentIntensity);
        if (!score.IsAnomalous(_options.RobustZScoreThreshold, _options.MinimumIntensityRelativeDeviation))
        {
            return RuleDecision.Passed(EmissionIntensityRule, samples.Length, scope);
        }

        return RuleDecision.Flagged(
            EmissionIntensityRule,
            samples.Length,
            scope,
            new AnomalyFinding(
                FindingCodes.SuspiciousEmissionIntensity,
                EmissionIntensityRule,
                "The CO2-to-energy relationship is inconsistent with prior clean observations.",
                Severity.High,
                new FindingEvidence(
                    "co2KgPerEnergyKwh",
                    decimal.Round(currentIntensity, 6),
                    decimal.Round(score.Median, 6),
                    score.RobustZScore,
                    $"maximum accepted relative deviation: {_options.MinimumIntensityRelativeDeviation:P0}")));
    }

    private static EvaluationState CreateInitialState(
        PreparedRecord record,
        HashSet<long> duplicateIds,
        HashSet<string> duplicateSiteMonths)
    {
        List<AnomalyFinding> findings = Validate(record, duplicateIds, duplicateSiteMonths);
        var state = new EvaluationState(findings);

        if (findings.Count == 0)
        {
            state.Evaluations.Add(new RuleEvaluation(DataQualityRule, "1.0", RuleOutcome.Passed, 0));
        }
        else
        {
            state.Evaluations.Add(new RuleEvaluation(
                DataQualityRule,
                "1.0",
                RuleOutcome.Flagged,
                0,
                Detail: $"{findings.Count} data quality issue(s) found."));
            state.Evaluations.Add(new RuleEvaluation(
                EnergyBehaviorRule,
                "2.0",
                RuleOutcome.NotEvaluated,
                0,
                Detail: "Skipped because data quality validation failed."));
            state.Evaluations.Add(new RuleEvaluation(
                EmissionIntensityRule,
                "2.0",
                RuleOutcome.NotEvaluated,
                0,
                Detail: "Skipped because data quality validation failed."));
        }

        return state;
    }

    private static BusinessEvent? FindApprovedContext(
        PreparedRecord current,
        decimal observedMultiplier,
        IEnumerable<BusinessEvent> businessEvents) =>
        businessEvents.FirstOrDefault(candidate =>
            candidate.Approved &&
            string.Equals(candidate.Site?.Trim(), current.Source.Site?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            TryParseMonth(candidate.EffectiveFrom, out DateOnly from) &&
            TryParseMonth(candidate.EffectiveTo, out DateOnly to) &&
            current.Period >= from &&
            current.Period <= to &&
            candidate.ExpectedEnergyMultiplierMin > 0m &&
            candidate.ExpectedEnergyMultiplierMax >= candidate.ExpectedEnergyMultiplierMin &&
            observedMultiplier >= candidate.ExpectedEnergyMultiplierMin &&
            observedMultiplier <= candidate.ExpectedEnergyMultiplierMax);

    private static RollingHistory GetOrCreateHistory(
        Dictionary<string, RollingHistory> histories,
        string key,
        int capacity)
    {
        if (!histories.TryGetValue(key, out RollingHistory? history))
        {
            history = new RollingHistory(capacity);
            histories.Add(key, history);
        }

        return history;
    }

    private DetectionPolicy BuildPolicy() => new(
        PolicyVersion,
        _options.RobustZScoreThreshold,
        _options.MinimumEnergyRelativeDeviation,
        _options.MinimumIntensityRelativeDeviation,
        _options.MinimumEnergyHistory,
        _options.MinimumSiteIntensityHistory,
        _options.MinimumGlobalIntensityHistory,
        _options.EnergyHistoryWindow,
        _options.SiteIntensityHistoryWindow,
        _options.GlobalIntensityHistoryWindow);

    private static PreparedRecord[] Prepare(IReadOnlyList<EmissionRecord> records) =>
        records.Select((record, index) => new PreparedRecord(
            index,
            record,
            record.Site?.Trim().ToUpperInvariant() ?? string.Empty,
            TryParseMonth(record.Month, out DateOnly period) ? period : null)).ToArray();

    private static List<AnomalyFinding> Validate(
        PreparedRecord record,
        HashSet<long> duplicateIds,
        HashSet<string> duplicateSiteMonths)
    {
        var findings = new List<AnomalyFinding>();

        AddValidationFinding(record.Source.Id <= 0, FindingCodes.InvalidId, "id", record.Source.Id, "Id must be greater than zero.", findings);
        AddValidationFinding(string.IsNullOrWhiteSpace(record.Source.Site), FindingCodes.MissingSite, "site", null, "Site is required.", findings);
        AddValidationFinding(record.Period is null, FindingCodes.InvalidMonth, "month", null, "Month must use the yyyy-MM format.", findings);
        AddValidationFinding(record.Source.EnergyKwh <= 0m, FindingCodes.InvalidEnergy, "energyKwh", record.Source.EnergyKwh, "Energy must be greater than zero.", findings);
        AddValidationFinding(record.Source.Co2Kg <= 0m, FindingCodes.InvalidCo2, "co2Kg", record.Source.Co2Kg, "CO2 emissions must be greater than zero.", findings);
        AddValidationFinding(duplicateIds.Contains(record.Source.Id), FindingCodes.DuplicateId, "id", record.Source.Id, "Id must be unique within the batch.", findings);

        string? siteMonthKey = record.Period is null || string.IsNullOrWhiteSpace(record.NormalizedSite)
            ? null
            : BuildSiteMonthKey(record.NormalizedSite, record.Period.Value);
        AddValidationFinding(
            siteMonthKey is not null && duplicateSiteMonths.Contains(siteMonthKey),
            FindingCodes.DuplicateSiteMonth,
            "siteMonth",
            null,
            "Only one record per site and month is allowed.",
            findings);

        return findings;
    }

    private static void AddValidationFinding(
        bool condition,
        string code,
        string metric,
        decimal? observed,
        string reason,
        List<AnomalyFinding> findings)
    {
        if (!condition)
        {
            return;
        }

        findings.Add(new AnomalyFinding(
            code,
            DataQualityRule,
            reason,
            Severity.Critical,
            new FindingEvidence(metric, observed)));
    }

    private static HashSet<long> FindDuplicateIds(IEnumerable<EmissionRecord> records) =>
        records.GroupBy(record => record.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

    private static HashSet<string> FindDuplicateSiteMonths(IEnumerable<PreparedRecord> records) =>
        records.Where(record => record.Period is not null && !string.IsNullOrWhiteSpace(record.NormalizedSite))
            .GroupBy(record => BuildSiteMonthKey(record.NormalizedSite, record.Period!.Value))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static string BuildSiteMonthKey(string normalizedSite, DateOnly period) =>
        $"{normalizedSite}|{period.ToString(MonthFormat, CultureInfo.InvariantCulture)}";

    private static bool TryParseMonth(string? value, out DateOnly period) =>
        DateOnly.TryParseExact(value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out period);

    private static AnalysisSummary BuildSummary(IReadOnlyCollection<DetectionResult> results) =>
        new(
            results.Count,
            results.Count(result => result.RequiresReview),
            results.Count(result => result.Status == AssessmentStatus.PartiallyEvaluated),
            results.Count(result => result.Status == AssessmentStatus.ExplainedByContext),
            results.Count(result => result.Severity == Severity.Critical),
            results.Count(result => result.Severity == Severity.High),
            results.Count(result => result.Severity == Severity.Medium));

    private sealed record PreparedRecord(
        int Index,
        EmissionRecord Source,
        string NormalizedSite,
        DateOnly? Period);

    private sealed class EvaluationState(List<AnomalyFinding> findings)
    {
        public List<AnomalyFinding> Findings { get; } = findings;

        public List<RuleEvaluation> Evaluations { get; } = [];

        public void Add(RuleDecision decision)
        {
            Decisions.Add(decision);
            Evaluations.Add(decision.Evaluation);
            if (decision.Finding is not null)
            {
                Findings.Add(decision.Finding);
            }
        }

        public bool CanLearnFrom(string rule) =>
            Evaluations.Single(evaluation => evaluation.Rule == rule).Outcome != RuleOutcome.Flagged;

        public bool ShouldResetBaseline(string rule) =>
            Decisions.Any(decision => decision.Evaluation.Rule == rule && decision.ResetBaseline);

        private List<RuleDecision> Decisions { get; } = [];

        public DetectionResult ToResult(EmissionRecord source)
        {
            bool requiresReview = Findings.Any(finding => finding.Severity >= Severity.Medium);
            Severity severity = Findings
                .Where(finding => finding.Severity >= Severity.Medium)
                .Select(finding => finding.Severity)
                .DefaultIfEmpty(Severity.Info)
                .Max();
            AssessmentStatus status = requiresReview
                ? AssessmentStatus.RequiresReview
                : Evaluations.Any(evaluation => evaluation.Outcome == RuleOutcome.NotEvaluated)
                    ? AssessmentStatus.PartiallyEvaluated
                    : Evaluations.Any(evaluation => evaluation.Outcome == RuleOutcome.ExplainedByContext)
                        ? AssessmentStatus.ExplainedByContext
                        : AssessmentStatus.NoAnomalyDetected;

            return new DetectionResult(
                source.Id,
                source.Site,
                source.Month,
                requiresReview,
                status,
                severity,
                Findings,
                Evaluations);
        }
    }

    private sealed record RuleDecision(
        RuleEvaluation Evaluation,
        AnomalyFinding? Finding = null,
        bool ResetBaseline = false)
    {
        public static RuleDecision Passed(string rule, int sampleSize, string? scope) =>
            new(new RuleEvaluation(rule, "2.0", RuleOutcome.Passed, sampleSize, scope));

        public static RuleDecision NotEvaluated(string rule, int sampleSize, string? scope, string detail) =>
            new(new RuleEvaluation(rule, "2.0", RuleOutcome.NotEvaluated, sampleSize, scope, detail));

        public static RuleDecision Explained(
            string rule,
            int sampleSize,
            string? scope,
            bool resetBaseline,
            AnomalyFinding finding) =>
            new(
                new RuleEvaluation(
                    rule,
                    "2.0",
                    RuleOutcome.ExplainedByContext,
                    sampleSize,
                    scope,
                    resetBaseline ? "Approved structural context starts a new baseline segment." : null),
                finding,
                resetBaseline);

        public static RuleDecision Flagged(string rule, int sampleSize, string? scope, AnomalyFinding finding) =>
            new(new RuleEvaluation(rule, "2.0", RuleOutcome.Flagged, sampleSize, scope), finding);
    }
}
