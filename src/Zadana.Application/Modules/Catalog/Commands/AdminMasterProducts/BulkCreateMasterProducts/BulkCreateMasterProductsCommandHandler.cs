using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.AdminMasterProducts.BulkCreateMasterProducts;

public class BulkCreateMasterProductsCommandHandler : IRequestHandler<BulkCreateMasterProductsCommand, AdminMasterProductBulkOperationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAdminMasterProductBulkOperationQueue _queue;

    public BulkCreateMasterProductsCommandHandler(IApplicationDbContext context, IAdminMasterProductBulkOperationQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public async Task<AdminMasterProductBulkOperationDto> Handle(BulkCreateMasterProductsCommand request, CancellationToken cancellationToken)
    {
        var adminExists = await _context.Users.AnyAsync(x => x.Id == request.AdminUserId, cancellationToken);
        if (!adminExists)
        {
            throw new NotFoundException("User", request.AdminUserId);
        }

        var existingOperation = await _context.AdminMasterProductBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AdminUserId == request.AdminUserId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingOperation is not null)
        {
            return new AdminMasterProductBulkOperationDto(
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

        var normalizedSlugs = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Slug))
            .Select(x => x.Slug!.Trim().ToLowerInvariant())
            .ToList();

        var duplicateSlugs = normalizedSlugs
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateSlugs.Count > 0)
        {
            throw new ValidationException("Duplicate slugs are not allowed in the same bulk request.");
        }

        var normalizedBarcodes = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Barcode))
            .Select(x => x.Barcode!.Trim().ToLowerInvariant())
            .ToList();

        var duplicateBarcodes = normalizedBarcodes
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateBarcodes.Count > 0)
        {
            throw new ValidationException("Duplicate barcodes are not allowed in the same bulk request.");
        }

        var items = request.Items
            .Select((item, index) => new AdminMasterProductBulkOperationItem(
                index + 1,
                item.NameAr,
                item.NameEn,
                item.Slug ?? string.Empty,
                item.Barcode,
                item.CategoryId,
                item.BrandId,
                item.UnitId,
                item.Status,
                item.DescriptionAr,
                item.DescriptionEn))
            .ToList();

        var operation = new AdminMasterProductBulkOperation(request.AdminUserId, request.IdempotencyKey, items);
        foreach (var item in items)
        {
            item.AttachToOperation(operation.Id);
        }

        _context.AdminMasterProductBulkOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(operation.Id, cancellationToken);

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
