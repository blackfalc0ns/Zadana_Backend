using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Orders.Queries.GetCartVendors;

public record GetCartVendorsQuery(CartActor Actor, int Limit = 20, int Offset = 0) : IRequest<CartAvailableVendorsDto>;

public class GetCartVendorsQueryHandler : IRequestHandler<GetCartVendorsQuery, CartAvailableVendorsDto>
{
    private readonly IApplicationDbContext _context;

    public GetCartVendorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartAvailableVendorsDto> Handle(GetCartVendorsQuery request, CancellationToken cancellationToken)
    {
        var address = await CartBranchSelectionSupport.ResolveDefaultAddressAsync(_context, request.Actor, cancellationToken);
        var cart = await CartLookup.FindCartAsync(
            _context,
            request.Actor.UserId,
            request.Actor.GuestId,
            cancellationToken,
            includeItems: true);

        if (cart == null || cart.Items.Count == 0)
        {
            var availableVendorRows = await _context.VendorProducts
                .AsNoTracking()
                .Where(product =>
                    product.Status == VendorProductStatus.Active &&
                    product.IsAvailable &&
                    product.StockQuantity > 0 &&
                    product.MasterProduct.Status == ProductStatus.Active &&
                    product.Vendor.Status == VendorStatus.Active)
                .Select(product => new CartAvailableVendorProductRow(
                    product.VendorId,
                    product.Vendor.BusinessNameAr,
                    product.Vendor.BusinessNameEn,
                    product.Vendor.LogoUrl,
                    product.MasterProductId,
                    product.VendorBranchId))
                .ToListAsync(cancellationToken);

            availableVendorRows = await FilterRowsForAddressBranchAsync(availableVendorRows, address, cancellationToken);

            var availableVendors = availableVendorRows
                .GroupBy(product => new
                {
                    product.VendorId,
                    product.BusinessNameAr,
                    product.BusinessNameEn,
                    product.LogoUrl
                })
                .Select(group => new CartAvailableVendorRow(
                    group.Key.VendorId,
                    group.Key.BusinessNameAr,
                    group.Key.BusinessNameEn,
                    group.Key.LogoUrl,
                    group.Select(item => item.MasterProductId).Distinct().Count()))
                .ToList();

            var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
                _context,
                availableVendors.Select(item => item.Id),
                cancellationToken);

            return ToOffsetResponse(
                availableVendors
                    .Where(item => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, item.Id).IsVisibleInCatalog)
                    .Select(item => new CartAvailableVendorDto(
                        item.Id,
                        PickLocalized(item.NameAr, item.NameEn),
                        item.LogoUrl,
                        item.ProductsCount))
                    .OrderByDescending(item => item.ProductsCount)
                    .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                request.Limit,
                request.Offset);
        }

        var productIds = cart.Items
            .Select(item => item.MasterProductId)
            .Distinct()
            .ToList();

        var vendorProductRows = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                productIds.Contains(product.MasterProductId) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new CartAvailableVendorProductRow(
                product.VendorId,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.Vendor.LogoUrl,
                product.MasterProductId,
                product.VendorBranchId))
            .ToListAsync(cancellationToken);

        // Intentionally NOT filtered by the customer's address city/branch here: every
        // vendor that carries at least one cart product must stay visible so the customer
        // can always pick a store. Whether a specific product is actually serviceable for
        // the customer's address is surfaced per-item as "unavailable" in CartProjection.
        var vendorRows = vendorProductRows
            .GroupBy(product => new
            {
                product.VendorId,
                product.BusinessNameAr,
                product.BusinessNameEn,
                product.LogoUrl
            })
            .Select(group => new CartAvailableVendorRow(
                group.Key.VendorId,
                group.Key.BusinessNameAr,
                group.Key.BusinessNameEn,
                group.Key.LogoUrl,
                group.Select(item => item.MasterProductId).Distinct().Count()))
            .ToList();

        var decisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            _context,
            vendorRows.Select(item => item.Id),
            cancellationToken);

        var vendors = vendorRows
            .Select(item =>
            {
                var decision = VendorCustomerAvailabilityPolicy.ResolveOrOffline(decisions, item.Id);
                return new CartAvailableVendorDto(
                    item.Id,
                    PickLocalized(item.NameAr, item.NameEn),
                    item.LogoUrl,
                    item.ProductsCount,
                    decision.IsOnlineNow,
                    decision.IsVisibleInCatalog ? null : decision.ReasonCode);
            })
            .OrderByDescending(item => item.ProductsCount)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ToOffsetResponse(vendors, request.Limit, request.Offset);
    }

    private static CartAvailableVendorsDto ToOffsetResponse(
        List<CartAvailableVendorDto> vendors,
        int limit,
        int offset)
    {
        var normalizedOffset = OffsetLimitPagination.NormalizeOffset(offset);
        var normalizedLimit = OffsetLimitPagination.NormalizeLimit(limit);
        var total = vendors.Count;

        return new CartAvailableVendorsDto(
            vendors
                .Skip(normalizedOffset)
                .Take(normalizedLimit)
                .ToList(),
            total,
            normalizedLimit,
            normalizedOffset,
            OffsetLimitPagination.HasMore(normalizedOffset, normalizedLimit, total));
    }

    private async Task<List<CartAvailableVendorProductRow>> FilterRowsForAddressBranchAsync(
        List<CartAvailableVendorProductRow> rows,
        Domain.Modules.Identity.Entities.CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        var selectedBranchIdByVendor = await CartBranchSelectionSupport.ResolveAddressBranchIdsByVendorAsync(
            _context,
            rows.Select(row => row.VendorId),
            address,
            cancellationToken);

        if (rows.Count == 0 || selectedBranchIdByVendor.Count == 0)
        {
            return rows;
        }

        return rows
            .GroupBy(row => new { row.VendorId, row.MasterProductId })
            .SelectMany(group =>
            {
                if (!selectedBranchIdByVendor.TryGetValue(group.Key.VendorId, out var selectedBranchId))
                {
                    return group;
                }

                if (!selectedBranchId.HasValue)
                {
                    return [];
                }

                var branchRows = group
                    .Where(row => row.VendorBranchId == selectedBranchId.Value)
                    .ToList();

                if (branchRows.Count > 0)
                {
                    return branchRows;
                }

                var hasBranchScopedInventory = group.Any(row => row.VendorBranchId.HasValue);
                return hasBranchScopedInventory
                    ? []
                    : group.Where(row => !row.VendorBranchId.HasValue);
            })
            .ToList();
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim() ?? fallback?.Trim() ?? string.Empty;
    }

    private sealed record CartAvailableVendorRow(
        Guid Id,
        string? NameAr,
        string? NameEn,
        string? LogoUrl,
        int ProductsCount);

    private sealed record CartAvailableVendorProductRow(
        Guid VendorId,
        string? BusinessNameAr,
        string? BusinessNameEn,
        string? LogoUrl,
        Guid MasterProductId,
        Guid? VendorBranchId);
}
