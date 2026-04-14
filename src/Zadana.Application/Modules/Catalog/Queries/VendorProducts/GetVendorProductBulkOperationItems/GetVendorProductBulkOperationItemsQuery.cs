using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductBulkOperationItems;

public record GetVendorProductBulkOperationItemsQuery(Guid OperationId, Guid VendorId) : IRequest<IReadOnlyList<VendorProductBulkOperationItemDto>>;

public class GetVendorProductBulkOperationItemsQueryHandler : IRequestHandler<GetVendorProductBulkOperationItemsQuery, IReadOnlyList<VendorProductBulkOperationItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductBulkOperationItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<VendorProductBulkOperationItemDto>> Handle(GetVendorProductBulkOperationItemsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.VendorProductBulkOperations
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.OperationId && x.VendorId == request.VendorId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("VendorProductBulkOperation", request.OperationId);
        }

        return await _context.VendorProductBulkOperationItems
            .AsNoTracking()
            .Where(x => x.OperationId == request.OperationId)
            .Include(x => x.MasterProduct)
            .OrderBy(x => x.RowNumber)
            .Select(x => new VendorProductBulkOperationItemDto(
                x.Id,
                x.RowNumber,
                x.MasterProductId,
                x.MasterProduct.NameAr,
                x.MasterProduct.NameEn,
                x.SellingPrice,
                x.CompareAtPrice,
                x.StockQty,
                x.VendorBranchId,
                x.Sku,
                x.MinOrderQty,
                x.MaxOrderQty,
                x.Status.ToString(),
                x.ErrorMessage,
                x.CreatedVendorProductId))
            .ToListAsync(cancellationToken);
    }
}
