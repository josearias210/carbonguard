namespace CarbonGuard.Core.Models;

public sealed record BusinessEvent(
    string? Site,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? Type,
    decimal ExpectedEnergyMultiplierMin,
    decimal ExpectedEnergyMultiplierMax,
    bool Approved,
    bool ResetsBaseline,
    string? Description);
