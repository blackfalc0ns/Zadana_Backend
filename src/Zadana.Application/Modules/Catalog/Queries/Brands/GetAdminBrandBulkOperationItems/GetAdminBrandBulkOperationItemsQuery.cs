using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperationItems;

public record GetAdminBrandBulkOperationItemsQuery(Guid OperationId, Guid AdminUserId) : IRequest<IReadOnlyList<AdminBrandBulkOperationItemDto>>;

public class GetAdminBrandBulkOperationItemsQueryHandler : IRequestHandler<GetAdminBrandBulkOperationItemsQuery, IReadOnlyList<AdminBrandBulkOperationItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBrandBulkOperationItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminBrandBulkOperationItemDto>> Handle(GetAdminBrandBulkOperationItemsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.AdminBrandBulkOperations
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.OperationId && x.AdminUserId == request.AdminUserId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("AdminBrandBulkOperation", request.OperationId);
        }

        return await _context.AdminBrandBulkOperationItems
            .AsNoTracking()
            .Where(x => x.OperationId == request.OperationId)
            .OrderBy(x => x.RowNumber)
            .Select(x => new AdminBrandBulkOperationItemDto(
                x.Id,
                x.RowNumber,
                x.NameAr,
                x.NameEn,
                x.LogoUrl,
                x.CategoryId,
                x.IsActive,
                x.Status.ToString(),
                x.ErrorMessage,
                x.CreatedBrandId))
            .ToListAsync(cancellationToken);
    }
}
