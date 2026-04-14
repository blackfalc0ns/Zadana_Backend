using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.AdminMasterProducts.GetAdminMasterProductBulkOperation;

public record GetAdminMasterProductBulkOperationQuery(Guid OperationId, Guid AdminUserId) : IRequest<AdminMasterProductBulkOperationDto>;

public class GetAdminMasterProductBulkOperationQueryHandler : IRequestHandler<GetAdminMasterProductBulkOperationQuery, AdminMasterProductBulkOperationDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminMasterProductBulkOperationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminMasterProductBulkOperationDto> Handle(GetAdminMasterProductBulkOperationQuery request, CancellationToken cancellationToken)
    {
        var operation = await _context.AdminMasterProductBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OperationId && x.AdminUserId == request.AdminUserId, cancellationToken)
            ?? throw new NotFoundException("AdminMasterProductBulkOperation", request.OperationId);

        return new AdminMasterProductBulkOperationDto(
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
