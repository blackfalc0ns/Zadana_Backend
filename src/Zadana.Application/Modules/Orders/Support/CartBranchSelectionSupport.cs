using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Modules.Orders.Support;

internal static class CartBranchSelectionSupport
{
    public static async Task<CustomerAddress?> ResolveDefaultAddressAsync(
        IApplicationDbContext context,
        CartActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.UserId.HasValue)
        {
            return null;
        }

        return await context.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.UserId == actor.UserId.Value)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.UpdatedAtUtc)
            .ThenByDescending(address => address.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<Guid, Guid?>> ResolveAddressBranchIdsByVendorAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> vendorIds,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        if (address is null)
        {
            return new Dictionary<Guid, Guid?>();
        }

        var distinctVendorIds = vendorIds.Distinct().ToArray();
        if (distinctVendorIds.Length == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Where(branch => distinctVendorIds.Contains(branch.VendorId) && branch.IsActive)
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .Select(branch => new AddressBranchCandidate(
                branch.VendorId,
                branch.Id,
                branch.Latitude,
                branch.Longitude,
                branch.DeliveryRadiusKm,
                branch.City,
                branch.IsPrimary,
                branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return branches
            .GroupBy(branch => branch.VendorId)
            .ToDictionary(
                group => group.Key,
                group => (Guid?)ResolveBestBranchForAddress(group.ToList(), address)?.Id);
    }

    private static AddressBranchCandidate? ResolveBestBranchForAddress(
        IReadOnlyCollection<AddressBranchCandidate> branches,
        CustomerAddress address)
    {
        if (branches.Count == 0)
        {
            return null;
        }

        var sameCityBranch = branches
            .Where(branch => DeliveryCityMatcher.Matches(branch.City, address.City))
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault();

        if (sameCityBranch is not null)
        {
            return sameCityBranch;
        }

        if (!string.IsNullOrWhiteSpace(address.City))
        {
            return null;
        }

        if (HasUsableCoordinates(address))
        {
            var addressLatitude = address.Latitude!.Value;
            var addressLongitude = address.Longitude!.Value;
            return branches
                .Where(branch => HasUsableCoordinates(branch.Latitude, branch.Longitude))
                .Select(branch =>
                {
                    var distanceKm = ApproximateDistanceKm(branch.Latitude, branch.Longitude, addressLatitude, addressLongitude);
                    var isInsideRadius = branch.DeliveryRadiusKm <= 0m || distanceKm <= (double)branch.DeliveryRadiusKm;
                    return new AddressBranchDistance(branch, distanceKm, isInsideRadius);
                })
                .Where(item => item.IsInsideRadius)
                .OrderBy(item => item.DistanceKm)
                .ThenByDescending(item => item.Branch.IsPrimary)
                .ThenBy(item => item.Branch.CreatedAtUtc)
                .Select(item => item.Branch)
                .FirstOrDefault();
        }

        return branches
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static bool HasUsableCoordinates(CustomerAddress address) =>
        address.Latitude.HasValue && address.Longitude.HasValue;

    private static bool HasUsableCoordinates(decimal? latitude, decimal? longitude) =>
        latitude.HasValue && longitude.HasValue;

    private static double ApproximateDistanceKm(decimal? fromLatitude, decimal? fromLongitude, decimal toLatitude, decimal toLongitude)
    {
        const double earthRadiusKm = 6371d;
        var lat1 = DegreesToRadians((double)fromLatitude!.Value);
        var lat2 = DegreesToRadians((double)toLatitude);
        var deltaLat = DegreesToRadians((double)(toLatitude - fromLatitude.Value));
        var deltaLon = DegreesToRadians((double)(toLongitude - fromLongitude!.Value));

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed record AddressBranchCandidate(
        Guid VendorId,
        Guid Id,
        decimal? Latitude,
        decimal? Longitude,
        decimal DeliveryRadiusKm,
        string? City,
        bool IsPrimary,
        DateTime CreatedAtUtc);

    private sealed record AddressBranchDistance(
        AddressBranchCandidate Branch,
        double DistanceKm,
        bool IsInsideRadius);
}
