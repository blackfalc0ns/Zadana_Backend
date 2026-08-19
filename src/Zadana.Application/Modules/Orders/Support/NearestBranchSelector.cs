using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Modules.Orders.Support;

public static class NearestBranchSelector
{
    public static IEnumerable<T> Order<T>(
        IReadOnlyCollection<T> branches,
        decimal? customerLatitude,
        decimal? customerLongitude,
        Func<T, decimal?> latitude,
        Func<T, decimal?> longitude,
        Func<T, bool> isPrimary,
        Func<T, DateTime> createdAtUtc)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        if (!GeoDistance.HasUsableCoordinates(customerLatitude, customerLongitude))
        {
            return branches
                .OrderByDescending(isPrimary)
                .ThenBy(createdAtUtc);
        }

        return branches
            .Where(branch => GeoDistance.HasUsableCoordinates(latitude(branch), longitude(branch)))
            .Select(branch =>
            {
                var distanceKm = GeoDistance.Kilometers(
                    latitude(branch)!.Value,
                    longitude(branch)!.Value,
                    customerLatitude!.Value,
                    customerLongitude!.Value);
                return (Branch: branch, DistanceKm: distanceKm);
            })
            .Where(item => item.DistanceKm <= DeliveryProximityLimits.MaxMatchKm)
            .OrderBy(item => item.DistanceKm)
            .ThenByDescending(item => isPrimary(item.Branch))
            .ThenBy(item => createdAtUtc(item.Branch))
            .Select(item => item.Branch);
    }
}
