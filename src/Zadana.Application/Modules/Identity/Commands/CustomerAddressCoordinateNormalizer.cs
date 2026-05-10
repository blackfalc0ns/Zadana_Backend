using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Identity.Commands;

internal static class CustomerAddressCoordinateNormalizer
{
    public static async Task<(decimal? Latitude, decimal? Longitude)> NormalizeAsync(
        IApplicationDbContext context,
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return (latitude, longitude);
        }

        if (latitude.Value == 0m && longitude.Value == 0m)
        {
            return (null, null);
        }

        var zones = await context.DeliveryZones
            .AsNoTracking()
            .Where(zone => zone.IsActive)
            .Select(zone => new ZonePoint(zone.CenterLat, zone.CenterLng, zone.RadiusKm))
            .ToArrayAsync(cancellationToken);

        if (zones.Length == 0 || IsInsideAnyZone(zones, latitude.Value, longitude.Value))
        {
            return (latitude, longitude);
        }

        if (IsInsideAnyZone(zones, longitude.Value, latitude.Value))
        {
            return (longitude, latitude);
        }

        return (latitude, longitude);
    }

    private static bool IsInsideAnyZone(IReadOnlyCollection<ZonePoint> zones, decimal latitude, decimal longitude) =>
        zones.Any(zone => ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude) <= zone.RadiusKm);

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;

        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        var distanceKm = Math.Sqrt(x * x + y * y) * 6371;

        return (decimal)distanceKm;
    }

    private sealed record ZonePoint(decimal CenterLat, decimal CenterLng, decimal RadiusKm);
}
