using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Resolves vendor/driver names from IDs for financial ledger display.
/// </summary>
public sealed class FinanceOwnerNameResolver
{
    private readonly IApplicationDbContext _context;

    public FinanceOwnerNameResolver(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> ResolveOwnerNameAsync(
        FinancialOwnerType? ownerType,
        Guid? ownerId,
        CancellationToken cancellationToken = default)
    {
        if (ownerType is null || ownerId is null)
        {
            return null;
        }

        return ownerType switch
        {
            FinancialOwnerType.Vendor => await ResolveVendorNameAsync(ownerId.Value, cancellationToken),
            FinancialOwnerType.Driver => await ResolveDriverNameAsync(ownerId.Value, cancellationToken),
            FinancialOwnerType.Customer => await ResolveCustomerNameAsync(ownerId.Value, cancellationToken),
            FinancialOwnerType.Platform => "Platform",
            _ => null
        };
    }

    public async Task<Dictionary<Guid, string>> BatchResolveVendorNamesAsync(
        IEnumerable<Guid> vendorIds,
        CancellationToken cancellationToken = default)
    {
        var ids = vendorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Vendors
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(
                v => v.Id,
                v => !string.IsNullOrWhiteSpace(v.BusinessNameAr)
                    ? v.BusinessNameAr
                    : !string.IsNullOrWhiteSpace(v.BusinessNameEn)
                        ? v.BusinessNameEn
                        : "Unknown Vendor",
                cancellationToken);
    }

    public async Task<Dictionary<Guid, string>> BatchResolveDriverNamesAsync(
        IEnumerable<Guid> driverIds,
        CancellationToken cancellationToken = default)
    {
        var ids = driverIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Drivers
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(
                d => d.Id,
                d => d.User.FullName ?? "Unknown Driver",
                cancellationToken);
    }

    private async Task<string?> ResolveVendorNameAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == vendorId)
            .Select(v => new { v.BusinessNameAr, v.BusinessNameEn })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor is null)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
            ? vendor.BusinessNameAr
            : !string.IsNullOrWhiteSpace(vendor.BusinessNameEn)
                ? vendor.BusinessNameEn
                : "Unknown Vendor";
    }

    private async Task<string?> ResolveDriverNameAsync(Guid driverId, CancellationToken cancellationToken)
    {
        var driver = await _context.Drivers
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Id == driverId)
            .Select(d => d.User.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return driver ?? "Unknown Driver";
    }

    private Task<string?> ResolveCustomerNameAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // Customers table not exposed in IApplicationDbContext yet
        return Task.FromResult<string?>("Customer");
    }
}
