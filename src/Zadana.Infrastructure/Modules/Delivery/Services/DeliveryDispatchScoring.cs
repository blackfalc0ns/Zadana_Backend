using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

internal sealed record DeliveryDispatchContext(
    DeliveryZone? PickupZone,
    string? PickupCity,
    decimal? PickupLatitude,
    decimal? PickupLongitude);

internal sealed record DeliveryDispatchCandidateEvaluation(
    int Tier,
    decimal CompositeScore,
    decimal DistanceKm,
    int ActiveTaskCount,
    decimal ReliabilityScore,
    bool GpsFresh,
    bool LowConfidenceGps,
    bool InPrimaryZone,
    bool InPickupZone,
    string MatchReason,
    string DistanceBucket);

internal static class DeliveryDispatchScoring
{
    internal static readonly TimeSpan GpsFreshnessThreshold = TimeSpan.FromMinutes(5);
    internal const decimal LowConfidenceAccuracyMeters = 100m;

    public static DeliveryDispatchContext BuildContext(
        IReadOnlyCollection<DeliveryZone> activeZones,
        decimal? pickupLatitude,
        decimal? pickupLongitude,
        string? fallbackCity = null)
    {
        DeliveryZone? pickupZone = null;
        var pickupCity = fallbackCity;

        if (pickupLatitude.HasValue && pickupLongitude.HasValue && activeZones.Count > 0)
        {
            pickupZone = ResolveContainingZone(activeZones, pickupLatitude.Value, pickupLongitude.Value)
                ?? ResolveNearestZone(activeZones, pickupLatitude.Value, pickupLongitude.Value);

            pickupCity ??= pickupZone?.City;
        }

        return new DeliveryDispatchContext(pickupZone, pickupCity, pickupLatitude, pickupLongitude);
    }

    public static DeliveryDispatchCandidateEvaluation EvaluateCandidate(
        Driver driver,
        DriverLocation? latestLocation,
        int activeTaskCount,
        decimal reliabilityScore,
        DeliveryDispatchContext context,
        DateTime utcNow)
    {
        var gpsFresh = latestLocation is not null && (utcNow - latestLocation.RecordedAtUtc) <= GpsFreshnessThreshold;
        var lowConfidenceGps = latestLocation?.AccuracyMeters > LowConfidenceAccuracyMeters;
        var sameZone = context.PickupZone is not null && driver.PrimaryZoneId == context.PickupZone.Id;
        var sameCity = !sameZone
            && !string.IsNullOrWhiteSpace(context.PickupCity)
            && string.Equals(driver.PrimaryZone?.City, context.PickupCity, StringComparison.OrdinalIgnoreCase);

        var inPrimaryZone = gpsFresh
            && latestLocation is not null
            && driver.PrimaryZone is not null
            && IsPointWithinZone(driver.PrimaryZone, latestLocation.Latitude, latestLocation.Longitude);

        var inPickupZone = gpsFresh
            && latestLocation is not null
            && context.PickupZone is not null
            && IsPointWithinZone(context.PickupZone, latestLocation.Latitude, latestLocation.Longitude);

        var distanceKm = ResolveDistanceKm(driver, latestLocation, context, gpsFresh, lowConfidenceGps);
        var distanceBucket = BuildDistanceBucket(distanceKm);
        var tier = ResolveTier(sameZone, sameCity, gpsFresh, lowConfidenceGps, inPrimaryZone, inPickupZone);
        var matchReason = ResolveMatchReason(tier);

        var freshnessPenalty = gpsFresh ? 0m : 80m;
        var accuracyPenalty = !gpsFresh || latestLocation?.AccuracyMeters is null
            ? 0m
            : latestLocation.AccuracyMeters.Value > LowConfidenceAccuracyMeters
                ? 60m
                : latestLocation.AccuracyMeters.Value > 30m
                    ? 12m
                    : 0m;

        var compositeScore = (tier * 1000m)
            + (distanceKm * 3m)
            + (activeTaskCount * 20m)
            + freshnessPenalty
            + accuracyPenalty
            - (reliabilityScore * 0.5m);

        return new DeliveryDispatchCandidateEvaluation(
            tier,
            Math.Round(compositeScore, 2),
            Math.Round(distanceKm, 2),
            activeTaskCount,
            Math.Round(reliabilityScore, 1),
            gpsFresh,
            lowConfidenceGps,
            inPrimaryZone,
            inPickupZone,
            matchReason,
            distanceBucket);
    }

    public static bool IsPointWithinZone(DeliveryZone zone, decimal latitude, decimal longitude) =>
        ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude) <= zone.RadiusKm;

    public static DeliveryZone? ResolveContainingZone(
        IReadOnlyCollection<DeliveryZone> zones,
        decimal latitude,
        decimal longitude) =>
        zones
            .Where(zone => zone.IsActive && IsPointWithinZone(zone, latitude, longitude))
            .OrderBy(zone => ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude))
            .FirstOrDefault();

    public static DeliveryZone? ResolveNearestZone(
        IReadOnlyCollection<DeliveryZone> zones,
        decimal latitude,
        decimal longitude) =>
        zones
            .Where(zone => zone.IsActive)
            .OrderBy(zone => ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude))
            .FirstOrDefault();

    public static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;

        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        var distanceKm = Math.Sqrt(x * x + y * y) * 6371;

        return (decimal)distanceKm;
    }

    private static int ResolveTier(
        bool sameZone,
        bool sameCity,
        bool gpsFresh,
        bool lowConfidenceGps,
        bool inPrimaryZone,
        bool inPickupZone) =>
        sameZone && gpsFresh && !lowConfidenceGps && (inPrimaryZone || inPickupZone) ? 1
        : sameZone ? 2
        : sameCity ? 3
        : 4;

    private static string ResolveMatchReason(int tier) =>
        tier switch
        {
            1 => "same-zone-live-gps",
            2 => "same-zone-stale-gps",
            3 => "same-city-fallback",
            _ => "out-of-zone-low-priority"
        };

    private static decimal ResolveDistanceKm(
        Driver driver,
        DriverLocation? latestLocation,
        DeliveryDispatchContext context,
        bool gpsFresh,
        bool lowConfidenceGps)
    {
        if (gpsFresh
            && !lowConfidenceGps
            && latestLocation is not null
            && context.PickupLatitude.HasValue
            && context.PickupLongitude.HasValue)
        {
            return ApproximateDistanceKm(
                latestLocation.Latitude,
                latestLocation.Longitude,
                context.PickupLatitude.Value,
                context.PickupLongitude.Value);
        }

        if (driver.PrimaryZone is not null && context.PickupZone is not null)
        {
            return driver.PrimaryZoneId == context.PickupZone.Id
                ? Math.Min(2m, context.PickupZone.RadiusKm / 2m)
                : ApproximateDistanceKm(
                    driver.PrimaryZone.CenterLat,
                    driver.PrimaryZone.CenterLng,
                    context.PickupZone.CenterLat,
                    context.PickupZone.CenterLng);
        }

        if (driver.PrimaryZone is not null
            && context.PickupLatitude.HasValue
            && context.PickupLongitude.HasValue)
        {
            return ApproximateDistanceKm(
                driver.PrimaryZone.CenterLat,
                driver.PrimaryZone.CenterLng,
                context.PickupLatitude.Value,
                context.PickupLongitude.Value);
        }

        return 99m;
    }

    private static string BuildDistanceBucket(decimal distanceKm) =>
        distanceKm switch
        {
            <= 2m => "nearby",
            <= 7m => "short-haul",
            <= 15m => "city-range",
            _ => "long-range"
        };
}
