using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public record UpdateVendorProductCommand(
    Guid Id,
    Guid VendorId,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int StockQty,
    string? CustomNameAr,
    string? CustomNameEn,
    string? CustomDescriptionAr,
    string? CustomDescriptionEn) : IRequest;
