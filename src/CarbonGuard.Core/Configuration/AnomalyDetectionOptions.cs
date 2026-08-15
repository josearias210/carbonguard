namespace CarbonGuard.Core.Configuration;

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";

    public int MinimumEnergyHistory { get; init; } = 3;

    public int MinimumSiteIntensityHistory { get; init; } = 3;

    public int MinimumGlobalIntensityHistory { get; init; } = 4;

    public int EnergyHistoryWindow { get; init; } = 24;

    public int SiteIntensityHistoryWindow { get; init; } = 24;

    public int GlobalIntensityHistoryWindow { get; init; } = 500;

    public decimal RobustZScoreThreshold { get; init; } = 3.5m;

    public decimal MinimumEnergyRelativeDeviation { get; init; } = 0.50m;

    public decimal MinimumIntensityRelativeDeviation { get; init; } = 0.50m;

    public bool IsValid() =>
        MinimumEnergyHistory >= 2 &&
        MinimumSiteIntensityHistory >= 2 &&
        MinimumGlobalIntensityHistory >= 2 &&
        EnergyHistoryWindow >= MinimumEnergyHistory &&
        SiteIntensityHistoryWindow >= MinimumSiteIntensityHistory &&
        GlobalIntensityHistoryWindow >= MinimumGlobalIntensityHistory &&
        RobustZScoreThreshold > 0 &&
        MinimumEnergyRelativeDeviation > 0 &&
        MinimumIntensityRelativeDeviation > 0;
}
