using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
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
                string.IsNullOrWhiteSpace(branch.City) ? branch.Vendor.City : branch.City,
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

        var ordered = NearestBranchSelector.Order(
            branches,
            address.Latitude,
            address.Longitude,
            branch => branch.Latitude,
            branch => branch.Longitude,
            branch => branch.IsPrimary,
            branch => branch.CreatedAtUtc).ToList();

        return ordered.Count > 0 ? ordered[0] : null;
    }

    private sealed record AddressBranchCandidate(
        Guid VendorId,
        Guid Id,
        decimal? Latitude,
        decimal? Longitude,
        decimal DeliveryRadiusKm,
        string? City,
        bool IsPrimary,
        DateTime CreatedAtUtc);
}
