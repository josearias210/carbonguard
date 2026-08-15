namespace CarbonGuard.Core.Models;

public sealed record DetectionPolicy(
    string Version,
    decimal RobustZScoreThreshold,
    decimal MinimumEnergyRelativeDeviation,
    decimal MinimumIntensityRelativeDeviation,
    int MinimumEnergyHistory,
    int MinimumSiteIntensityHistory,
    int MinimumGlobalIntensityHistory,
    int EnergyHistoryWindow,
    int SiteIntensityHistoryWindow,
    int GlobalIntensityHistoryWindow);
