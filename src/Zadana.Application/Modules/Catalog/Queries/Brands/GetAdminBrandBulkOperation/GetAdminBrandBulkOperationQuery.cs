using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandBulkOperation;

public record GetAdminBrandBulkOperationQuery(Guid OperationId, Guid AdminUserId) : IRequest<AdminBrandBulkOperationDto>;

public class GetAdminBrandBulkOperationQueryHandler : IRequestHandler<GetAdminBrandBulkOperationQuery, AdminBrandBulkOperationDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBrandBulkOperationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminBrandBulkOperationDto> Handle(GetAdminBrandBulkOperationQuery request, CancellationToken cancellationToken)
    {
        var operation = await _context.AdminBrandBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OperationId && x.AdminUserId == request.AdminUserId, cancellationToken)
            ?? throw new NotFoundException("AdminBrandBulkOperation", request.OperationId);

        return new AdminBrandBulkOperationDto(
            operation.Id,
            operation.IdempotencyKey,
            operation.Status.ToString(),
            operation.TotalRows,
            operation.ProcessedRows,
            operation.SucceededRows,
            operation.FailedRows,
            operation.ErrorMessage,
            operation.CreatedAtUtc,
            operation.StartedAtUtc,
            operation.CompletedAtUtc);
    }
}
