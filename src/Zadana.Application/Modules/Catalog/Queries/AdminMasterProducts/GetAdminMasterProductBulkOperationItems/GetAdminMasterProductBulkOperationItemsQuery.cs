using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.AdminMasterProducts.GetAdminMasterProductBulkOperationItems;

public record GetAdminMasterProductBulkOperationItemsQuery(Guid OperationId, Guid AdminUserId) : IRequest<IReadOnlyList<AdminMasterProductBulkOperationItemDto>>;

public class GetAdminMasterProductBulkOperationItemsQueryHandler : IRequestHandler<GetAdminMasterProductBulkOperationItemsQuery, IReadOnlyList<AdminMasterProductBulkOperationItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminMasterProductBulkOperationItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminMasterProductBulkOperationItemDto>> Handle(GetAdminMasterProductBulkOperationItemsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.AdminMasterProductBulkOperations
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.OperationId && x.AdminUserId == request.AdminUserId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("AdminMasterProductBulkOperation", request.OperationId);
        }

        return await _context.AdminMasterProductBulkOperationItems
            .AsNoTracking()
            .Where(x => x.OperationId == request.OperationId)
            .OrderBy(x => x.RowNumber)
            .Select(x => new AdminMasterProductBulkOperationItemDto(
                x.Id,
                x.RowNumber,
                x.NameAr,
                x.NameEn,
                x.Slug,
                x.Barcode,
                x.CategoryId,
                x.BrandId,
                x.UnitId,
                x.StatusValue.ToString(),
                x.DescriptionAr,
                x.DescriptionEn,
                x.Status.ToString(),
                x.ErrorMessage,
                x.CreatedMasterProductId))
            .ToListAsync(cancellationToken);
    }
}
