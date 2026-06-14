using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductById;

public record GetVendorProductByIdQuery(Guid VendorId, Guid ProductId, Guid? BranchId = null) : IRequest<VendorProductDto>;

public class GetVendorProductByIdQueryHandler : IRequestHandler<GetVendorProductByIdQuery, VendorProductDto>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorProductDto> Handle(GetVendorProductByIdQuery request, CancellationToken cancellationToken)
    {
        var vp = await _context.VendorProducts
            .AsNoTracking()
            .Include(x => x.MasterProduct)
                .ThenInclude(mp => mp.Images)
            .Include(x => x.MasterProduct)
                .ThenInclude(mp => mp.Brand)
            .Include(x => x.MasterProduct)
                .ThenInclude(mp => mp.PackageType)
            .Include(x => x.MasterProduct)
                .ThenInclude(mp => mp.MeasurementUnit)
            .Include(x => x.Vendor)
            .Include(x => x.VendorBranch)
            .FirstOrDefaultAsync(x =>
                x.Id == request.ProductId &&
                x.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || x.VendorBranchId == request.BranchId.Value),
                cancellationToken);

        if (vp == null)
            throw new NotFoundException("VendorProduct", request.ProductId);

        return new VendorProductDto(
            vp.Id,
            vp.VendorId,
            vp.MasterProductId,
            vp.CostPrice,
            vp.TradePrice,
            vp.SellingPrice,
            vp.CompareAtPrice,
            vp.Vendor.CommissionRate,
            vp.StockQuantity,
            vp.IsAvailable,
            vp.Status.ToString(),
            MasterProductDisplayDto.ToDto(vp.MasterProduct, true),
            vp.VendorBranchId,
            vp.VendorBranchId is null || vp.VendorBranch != null && vp.VendorBranch.IsPrimary
        );
    }
}
