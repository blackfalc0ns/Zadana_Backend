using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

public sealed class AdminBrandBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdminBrandBulkOperationQueue _queue;
    private readonly ILogger<AdminBrandBulkOperationWorker> _logger;

    public AdminBrandBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IAdminBrandBulkOperationQueue queue,
        ILogger<AdminBrandBulkOperationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var operationId = await _queue.DequeueAsync(stoppingToken);

            try
            {
                await ProcessOperationAsync(operationId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process admin brand bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var operation = await context.AdminBrandBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null)
        {
            return;
        }

        operation.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var categoryIds = operation.Items.Select(x => x.CategoryId).Distinct().ToArray();
            var categories = await context.Categories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var item in operation.Items.OrderBy(x => x.RowNumber))
            {
                if (item.Status != AdminBrandBulkOperationItemStatus.Pending)
                {
                    continue;
                }

                if (!categories.TryGetValue(item.CategoryId, out var category))
                {
                    item.MarkFailed("Category was not found.");
                }
                else if (!category.ParentCategoryId.HasValue)
                {
                    item.MarkFailed("Category must be a subcategory.");
                }
                else
                {
                    try
                    {
                        var brand = new Brand(item.NameAr, item.NameEn, item.LogoUrl, item.CategoryId);
                        if (!item.IsActive)
                        {
                            brand.Deactivate();
                        }

                        context.Brands.Add(brand);
                        await context.SaveChangesAsync(cancellationToken);
                        item.MarkSucceeded(brand.Id);
                    }
                    catch (DbUpdateException ex)
                    {
                        item.MarkFailed(ex.InnerException?.Message ?? "Brand could not be created.");
                    }
                    catch (Exception ex)
                    {
                        item.MarkFailed(ex.Message);
                    }
                }

                operation.RecalculateProgress();
                await context.SaveChangesAsync(cancellationToken);
            }

            operation.RecalculateProgress();
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            operation.MarkFailed(ex.Message);
            await context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
