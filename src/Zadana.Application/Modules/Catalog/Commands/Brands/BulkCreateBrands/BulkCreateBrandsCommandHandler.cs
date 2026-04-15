using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.BulkCreateBrands;

public class BulkCreateBrandsCommandHandler : IRequestHandler<BulkCreateBrandsCommand, AdminBrandBulkOperationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAdminBrandBulkOperationQueue _queue;

    public BulkCreateBrandsCommandHandler(IApplicationDbContext context, IAdminBrandBulkOperationQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public async Task<AdminBrandBulkOperationDto> Handle(BulkCreateBrandsCommand request, CancellationToken cancellationToken)
    {
        var adminExists = await _context.Users.AnyAsync(x => x.Id == request.AdminUserId, cancellationToken);
        if (!adminExists)
        {
            throw new NotFoundException("User", request.AdminUserId);
        }

        var existingOperation = await _context.AdminBrandBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AdminUserId == request.AdminUserId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingOperation is not null)
        {
            return new AdminBrandBulkOperationDto(
                existingOperation.Id,
                existingOperation.IdempotencyKey,
                existingOperation.Status.ToString(),
                existingOperation.TotalRows,
                existingOperation.ProcessedRows,
                existingOperation.SucceededRows,
                existingOperation.FailedRows,
                existingOperation.ErrorMessage,
                existingOperation.CreatedAtUtc,
                existingOperation.StartedAtUtc,
                existingOperation.CompletedAtUtc);
        }

        var items = request.Items
            .Select((item, index) => new AdminBrandBulkOperationItem(
                index + 1,
                item.NameAr,
                item.NameEn,
                item.LogoUrl,
                item.CategoryId,
                item.IsActive))
            .ToList();

        var operation = new AdminBrandBulkOperation(request.AdminUserId, request.IdempotencyKey, items);
        foreach (var item in items)
        {
            item.AttachToOperation(operation.Id);
        }

        _context.AdminBrandBulkOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(operation.Id, cancellationToken);

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
