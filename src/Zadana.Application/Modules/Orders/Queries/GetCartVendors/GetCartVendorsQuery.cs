using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Orders.Queries.GetCartVendors;

public record GetCartVendorsQuery(CartActor Actor) : IRequest<CartAvailableVendorsDto>;

public class GetCartVendorsQueryHandler : IRequestHandler<GetCartVendorsQuery, CartAvailableVendorsDto>
{
    private readonly IApplicationDbContext _context;

    public GetCartVendorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartAvailableVendorsDto> Handle(GetCartVendorsQuery request, CancellationToken cancellationToken)
    {
        var vendorRows = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders)
            .GroupBy(product => new
            {
                product.VendorId,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.Vendor.LogoUrl
            })
            .Select(group => new CartAvailableVendorRow(
                group.Key.VendorId,
                group.Key.BusinessNameAr,
                group.Key.BusinessNameEn,
                group.Key.LogoUrl,
                group.Select(item => item.MasterProductId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var vendors = vendorRows
            .Select(item => new CartAvailableVendorDto(
                item.Id,
                PickLocalized(item.NameAr, item.NameEn),
                item.LogoUrl,
                item.ProductsCount))
            .OrderByDescending(item => item.ProductsCount)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new CartAvailableVendorsDto(vendors);
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
}
