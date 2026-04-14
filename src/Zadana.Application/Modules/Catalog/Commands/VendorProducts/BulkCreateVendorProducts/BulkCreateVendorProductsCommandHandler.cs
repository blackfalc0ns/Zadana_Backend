using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.BulkCreateVendorProducts;

public class BulkCreateVendorProductsCommandHandler : IRequestHandler<BulkCreateVendorProductsCommand, VendorProductBulkOperationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVendorProductBulkOperationQueue _queue;

    public BulkCreateVendorProductsCommandHandler(IApplicationDbContext context, IVendorProductBulkOperationQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public async Task<VendorProductBulkOperationDto> Handle(BulkCreateVendorProductsCommand request, CancellationToken cancellationToken)
    {
        var vendorExists = await _context.Vendors.AnyAsync(v => v.Id == request.VendorId, cancellationToken);
        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", request.VendorId);
        }

        var existingOperation = await _context.VendorProductBulkOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey && x.VendorId == request.VendorId, cancellationToken);

        if (existingOperation is not null)
        {
            return new VendorProductBulkOperationDto(
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

        var duplicateMasterProducts = request.Items
            .GroupBy(x => x.MasterProductId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet();

        if (duplicateMasterProducts.Count > 0)
        {
            throw new ValidationException("Duplicate master products are not allowed in the same bulk request.");
        }

        var items = request.Items
            .Select((item, index) => new VendorProductBulkOperationItem(
                index + 1,
                item.MasterProductId,
                item.SellingPrice,
                item.CompareAtPrice,
                item.StockQty,
                item.BranchId,
                item.Sku,
                item.MinOrderQty,
                item.MaxOrderQty))
            .ToList();

        var operation = new VendorProductBulkOperation(request.VendorId, request.IdempotencyKey, items);
        foreach (var item in items)
        {
            item.AttachToOperation(operation.Id);
        }

        _context.VendorProductBulkOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(operation.Id, cancellationToken);

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
