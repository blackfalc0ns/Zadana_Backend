namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryProximityLimits
{
    public const decimal MaxMatchKm = 50m;
    public static readonly TimeSpan GpsFreshnessThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Expanding dispatch rings (km). Dispatch tries the smallest ring with eligible drivers first.
    /// </summary>
    public static readonly decimal[] DispatchSearchRingsKm =
    [
        5m, 10m, 12m, 15m, 20m, 25m, 30m, 35m, 40m, 45m, MaxMatchKm
    ];

    /// <summary>
    /// Effective delivery radius for a branch: uses branch radius when configured, capped at platform max.
    /// </summary>
    public static decimal ResolveEffectiveDeliveryRadiusKm(decimal branchDeliveryRadiusKm) =>
        branchDeliveryRadiusKm > 0m
            ? Math.Min(branchDeliveryRadiusKm, MaxMatchKm)
            : MaxMatchKm;
}
