using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

public sealed class AdminMasterProductBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdminMasterProductBulkOperationQueue _queue;
    private readonly ILogger<AdminMasterProductBulkOperationWorker> _logger;

    public AdminMasterProductBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IAdminMasterProductBulkOperationQueue queue,
        ILogger<AdminMasterProductBulkOperationWorker> logger)
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
                _logger.LogError(ex, "Failed to process admin master product bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var operation = await context.AdminMasterProductBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null)
        {
            return;
        }

        operation.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        var categoryIds = operation.Items.Select(x => x.CategoryId).Distinct().ToArray();
        var brandIds = operation.Items.Where(x => x.BrandId.HasValue).Select(x => x.BrandId!.Value).Distinct().ToArray();
        var unitIds = operation.Items.Where(x => x.UnitId.HasValue).Select(x => x.UnitId!.Value).Distinct().ToArray();

        var existingCategoryIds = await context.Categories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var parentCategoryIds = await context.Categories
            .AsNoTracking()
            .Where(x => x.ParentCategoryId.HasValue && categoryIds.Contains(x.ParentCategoryId.Value))
            .Select(x => x.ParentCategoryId!.Value)
            .ToHashSetAsync(cancellationToken);

        var existingBrandIds = await context.Brands
            .AsNoTracking()
            .Where(x => brandIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var existingUnitIds = await context.UnitsOfMeasure
            .AsNoTracking()
            .Where(x => unitIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var existingSlugs = await context.MasterProducts
            .AsNoTracking()
            .Select(x => x.Slug.ToLower())
            .ToHashSetAsync(cancellationToken);

        var existingBarcodes = await context.MasterProducts
            .AsNoTracking()
            .Where(x => x.Barcode != null)
            .Select(x => x.Barcode!.ToLower())
            .ToHashSetAsync(cancellationToken);

        var reservedSlugs = new HashSet<string>(existingSlugs);
        var reservedBarcodes = new HashSet<string>(existingBarcodes);

        foreach (var item in operation.Items.OrderBy(x => x.RowNumber))
        {
            if (item.Status != AdminMasterProductBulkOperationItemStatus.Pending)
            {
                continue;
            }

            if (!existingCategoryIds.Contains(item.CategoryId))
            {
                item.MarkFailed("Category was not found.");
            }
            else if (parentCategoryIds.Contains(item.CategoryId))
            {
                item.MarkFailed("Category is not a leaf category.");
            }
            else if (item.BrandId.HasValue && !existingBrandIds.Contains(item.BrandId.Value))
            {
                item.MarkFailed("Brand was not found.");
            }
            else if (item.UnitId.HasValue && !existingUnitIds.Contains(item.UnitId.Value))
            {
                item.MarkFailed("Unit was not found.");
            }
            else
            {
                try
                {
                    var generatedSlug = GenerateUniqueSlug(item, reservedSlugs);
                    var generatedBarcode = GenerateUniqueBarcode(item.Barcode, reservedBarcodes);

                    item.UpdateGeneratedValues(generatedSlug, generatedBarcode);

                    var masterProduct = new MasterProduct(
                        nameAr: item.NameAr,
                        nameEn: item.NameEn,
                        slug: generatedSlug,
                        categoryId: item.CategoryId,
                        brandId: item.BrandId,
                        unitOfMeasureId: item.UnitId,
                        descriptionAr: item.DescriptionAr,
                        descriptionEn: item.DescriptionEn,
                        barcode: generatedBarcode);

                    masterProduct.SetStatus(item.StatusValue);

                    var images = DeserializeImages(item.ImagesJson);
                    foreach (var image in images.OrderBy(x => x.DisplayOrder))
                    {
                        masterProduct.AddImage(image.Url, image.AltText, image.DisplayOrder, image.IsPrimary);
                    }

                    context.MasterProducts.Add(masterProduct);
                    await context.SaveChangesAsync(cancellationToken);

                    item.MarkSucceeded(masterProduct.Id);
                }
                catch (DbUpdateException)
                {
                    item.MarkFailed("Product conflicts with an existing slug or barcode.");
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

    private static string GenerateUniqueSlug(AdminMasterProductBulkOperationItem item, HashSet<string> reservedSlugs)
    {
        var baseSource = !string.IsNullOrWhiteSpace(item.Slug)
            ? item.Slug!
            : !string.IsNullOrWhiteSpace(item.NameEn)
                ? item.NameEn
                : item.NameAr;

        var normalized = Slugify(baseSource);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"product-{item.RowNumber}";
        }

        var candidate = normalized;
        var suffix = 1;
        while (reservedSlugs.Contains(candidate))
        {
            suffix += 1;
            candidate = $"{normalized}-{suffix}";
        }

        reservedSlugs.Add(candidate);
        return candidate;
    }

    private static string? GenerateUniqueBarcode(string? currentBarcode, HashSet<string> reservedBarcodes)
    {
        if (!string.IsNullOrWhiteSpace(currentBarcode))
        {
            var normalized = currentBarcode.Trim();
            var lowered = normalized.ToLowerInvariant();
            if (reservedBarcodes.Contains(lowered))
            {
                throw new InvalidOperationException("Barcode already exists.");
            }

            reservedBarcodes.Add(lowered);
            return normalized;
        }

        string generated;
        string loweredGenerated;
        do
        {
            generated = $"MP-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
            loweredGenerated = generated.ToLowerInvariant();
        }
        while (reservedBarcodes.Contains(loweredGenerated));

        reservedBarcodes.Add(loweredGenerated);
        return generated;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        var lastWasDash = false;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || (ch >= 0x0600 && ch <= 0x06FF))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static IReadOnlyList<AdminMasterProductBulkOperationItemImage> DeserializeImages(string? imagesJson)
    {
        if (string.IsNullOrWhiteSpace(imagesJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<AdminMasterProductBulkOperationItemImage>>(imagesJson) ?? [];
    }
}
