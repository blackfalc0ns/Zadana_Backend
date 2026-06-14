using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductBulkOperation;

public record GetVendorProductBulkOperationQuery(Guid OperationId, Guid VendorId, Guid? BranchId = null) : IRequest<VendorProductBulkOperationDto>;

public class GetVendorProductBulkOperationQueryHandler : IRequestHandler<GetVendorProductBulkOperationQuery, VendorProductBulkOperationDto>
{
    private static readonly TimeSpan ProcessingRecoveryAge = TimeSpan.FromMinutes(5);
    private readonly IApplicationDbContext _context;
    private readonly IVendorProductBulkOperationProcessor _processor;

    public GetVendorProductBulkOperationQueryHandler(IApplicationDbContext context, IVendorProductBulkOperationProcessor processor)
    {
        _context = context;
        _processor = processor;
    }

    public async Task<VendorProductBulkOperationDto> Handle(GetVendorProductBulkOperationQuery request, CancellationToken cancellationToken)
    {
        var operation = await _context.VendorProductBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.OperationId &&
                x.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || x.Items.Any(item => item.VendorBranchId == request.BranchId.Value)),
                cancellationToken)
            ?? throw new NotFoundException("VendorProductBulkOperation", request.OperationId);

        if (ShouldRecover(operation.Status, operation.StartedAtUtc))
        {
            await _processor.ProcessOperationAsync(operation.Id, cancellationToken);

            operation = await _context.VendorProductBulkOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.OperationId &&
                    x.VendorId == request.VendorId &&
                    (!request.BranchId.HasValue || x.Items.Any(item => item.VendorBranchId == request.BranchId.Value)),
                    cancellationToken)
                ?? throw new NotFoundException("VendorProductBulkOperation", request.OperationId);
        }

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

    private static bool ShouldRecover(VendorProductBulkOperationStatus status, DateTime? startedAtUtc)
        => status == VendorProductBulkOperationStatus.Pending ||
           (status == VendorProductBulkOperationStatus.Processing &&
            startedAtUtc.HasValue &&
            startedAtUtc.Value <= DateTime.UtcNow.Subtract(ProcessingRecoveryAge));
}
