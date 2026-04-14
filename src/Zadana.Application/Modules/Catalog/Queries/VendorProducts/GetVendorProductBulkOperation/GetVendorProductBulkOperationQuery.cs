using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductBulkOperation;

public record GetVendorProductBulkOperationQuery(Guid OperationId, Guid VendorId) : IRequest<VendorProductBulkOperationDto>;

public class GetVendorProductBulkOperationQueryHandler : IRequestHandler<GetVendorProductBulkOperationQuery, VendorProductBulkOperationDto>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductBulkOperationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorProductBulkOperationDto> Handle(GetVendorProductBulkOperationQuery request, CancellationToken cancellationToken)
    {
        var operation = await _context.VendorProductBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OperationId && x.VendorId == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("VendorProductBulkOperation", request.OperationId);

        return new VendorProductBulkOperationDto(
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
