namespace CarbonGuard.Core.Models;

public sealed record EmissionRecord(
    long Id,
    string? Site,
    string? Month,
    decimal EnergyKwh,
    decimal Co2Kg);

