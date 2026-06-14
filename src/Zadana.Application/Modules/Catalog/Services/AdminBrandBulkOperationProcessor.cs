using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Services;

public sealed class AdminBrandBulkOperationProcessor : IAdminBrandBulkOperationProcessor
{
    private readonly IApplicationDbContext _context;

    public AdminBrandBulkOperationProcessor(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _context.AdminBrandBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null || operation.Status is AdminBrandBulkOperationStatus.Completed or AdminBrandBulkOperationStatus.CompletedWithErrors or AdminBrandBulkOperationStatus.Failed)
        {
            return;
        }

        operation.MarkProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var categoryIds = operation.Items
                .SelectMany(x => x.GetCategoryIds())
                .Distinct()
                .ToArray();

            var categories = await _context.Categories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var item in operation.Items.OrderBy(x => x.RowNumber))
            {
                if (item.Status != AdminBrandBulkOperationItemStatus.Pending)
                {
                    continue;
                }

                var itemCategoryIds = item.GetCategoryIds();
                var missingCategoryIds = itemCategoryIds.Where(categoryId => !categories.ContainsKey(categoryId)).ToArray();
                var rootCategoryIds = itemCategoryIds
                    .Where(categoryId => categories.TryGetValue(categoryId, out var category) && !category.ParentCategoryId.HasValue)
                    .ToArray();

                if (missingCategoryIds.Length > 0)
                {
                    item.MarkFailed("Category was not found.");
                }
                else if (rootCategoryIds.Length > 0)
                {
                    item.MarkFailed("Category must be a subcategory.");
                }
                else
                {
                    await CreateBrandAsync(item, itemCategoryIds, cancellationToken);
                }

                operation.RecalculateProgress();
                await _context.SaveChangesAsync(cancellationToken);
            }

            operation.RecalculateProgress();
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            operation.MarkFailed(ex.Message);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task CreateBrandAsync(AdminBrandBulkOperationItem item, IReadOnlyList<Guid> itemCategoryIds, CancellationToken cancellationToken)
    {
        Brand? brand = null;

        try
        {
            brand = new Brand(item.NameAr, item.NameEn, item.LogoUrl, item.CoverImageUrl, itemCategoryIds[0]);
            if (!item.IsActive)
            {
                brand.Deactivate();
            }

            _context.Brands.Add(brand);
            foreach (var categoryId in itemCategoryIds)
            {
                _context.BrandCategories.Add(new BrandCategory(brand.Id, categoryId));
            }

            await _context.SaveChangesAsync(cancellationToken);
            item.MarkSucceeded(brand.Id);
        }
        catch (DbUpdateException ex)
        {
            DetachIfPossible(brand);
            item.MarkFailed(ex.InnerException?.Message ?? "Brand could not be created.");
        }
        catch (Exception ex)
        {
            DetachIfPossible(brand);
            item.MarkFailed(ex.Message);
        }
    }

    private void DetachIfPossible(object? entity)
    {
        if (entity is not null && _context is DbContext dbContext)
        {
            dbContext.Entry(entity).State = EntityState.Detached;
        }
    }
}
