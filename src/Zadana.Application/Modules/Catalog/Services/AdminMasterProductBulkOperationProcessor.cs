using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Services;

public sealed class AdminMasterProductBulkOperationProcessor : IAdminMasterProductBulkOperationProcessor
{
    private readonly IApplicationDbContext _context;

    public AdminMasterProductBulkOperationProcessor(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _context.AdminMasterProductBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null || operation.Status is AdminMasterProductBulkOperationStatus.Completed or AdminMasterProductBulkOperationStatus.CompletedWithErrors or AdminMasterProductBulkOperationStatus.Failed)
        {
            return;
        }

        operation.MarkProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        var categoryIds = operation.Items.Select(x => x.CategoryId).Distinct().ToArray();
        var brandIds = operation.Items.Where(x => x.BrandId.HasValue).Select(x => x.BrandId!.Value).Distinct().ToArray();
        var unitIds = operation.Items
            .SelectMany(x => new[] { x.UnitId, x.PackageTypeId, x.MeasurementUnitId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var existingCategoryIds = await _context.Categories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var parentCategoryIds = await _context.Categories
            .AsNoTracking()
            .Where(x => x.ParentCategoryId.HasValue && categoryIds.Contains(x.ParentCategoryId.Value))
            .Select(x => x.ParentCategoryId!.Value)
            .ToHashSetAsync(cancellationToken);

        var existingBrandIds = await _context.Brands
            .AsNoTracking()
            .Where(x => brandIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var existingUnits = await _context.UnitsOfMeasure
            .AsNoTracking()
            .Where(x => unitIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var reservedSlugs = await _context.MasterProducts
            .AsNoTracking()
            .Select(x => x.Slug.ToLower())
            .ToHashSetAsync(cancellationToken);

        var reservedBarcodes = await _context.MasterProducts
            .AsNoTracking()
            .Where(x => x.Barcode != null)
            .Select(x => x.Barcode!.ToLower())
            .ToHashSetAsync(cancellationToken);

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
            else if (item.UnitId.HasValue && !existingUnits.ContainsKey(item.UnitId.Value))
            {
                item.MarkFailed("Unit was not found.");
            }
            else if (item.PackageTypeId.HasValue && !existingUnits.ContainsKey(item.PackageTypeId.Value))
            {
                item.MarkFailed("Package type was not found.");
            }
            else if (item.MeasurementUnitId.HasValue && !existingUnits.ContainsKey(item.MeasurementUnitId.Value))
            {
                item.MarkFailed("Measurement unit was not found.");
            }
            else
            {
                await CreateMasterProductAsync(item, existingUnits, reservedSlugs, reservedBarcodes, cancellationToken);
            }

            operation.RecalculateProgress();
            await _context.SaveChangesAsync(cancellationToken);
        }

        operation.RecalculateProgress();
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateMasterProductAsync(
        AdminMasterProductBulkOperationItem item,
        IReadOnlyDictionary<Guid, UnitOfMeasure> existingUnits,
        HashSet<string> reservedSlugs,
        HashSet<string> reservedBarcodes,
        CancellationToken cancellationToken)
    {
        MasterProduct? masterProduct = null;

        try
        {
            var measurementUnitId = item.MeasurementUnitId ?? item.UnitId;
            if (measurementUnitId.HasValue && existingUnits[measurementUnitId.Value].Kind != UnitKind.Measurement)
            {
                throw new ValidationException("measurementUnitId must refer to a measurement unit.");
            }

            if (item.PackageTypeId.HasValue && existingUnits[item.PackageTypeId.Value].Kind != UnitKind.Packaging)
            {
                throw new ValidationException("packageTypeId must refer to a packaging unit.");
            }

            var generatedSlug = GenerateUniqueSlug(item, reservedSlugs);
            var generatedBarcode = GenerateUniqueBarcode(item.Barcode, reservedBarcodes);

            item.UpdateGeneratedValues(generatedSlug, generatedBarcode);

            masterProduct = new MasterProduct(
                nameAr: item.NameAr,
                nameEn: item.NameEn,
                slug: generatedSlug,
                categoryId: item.CategoryId,
                brandId: item.BrandId,
                unitOfMeasureId: measurementUnitId,
                packageTypeId: item.PackageTypeId,
                measurementValue: item.MeasurementValue,
                measurementUnitId: measurementUnitId,
                descriptionAr: item.DescriptionAr,
                descriptionEn: item.DescriptionEn,
                barcode: generatedBarcode,
                variantGroupId: item.VariantGroupId);

            masterProduct.SetStatus(item.StatusValue);

            foreach (var image in DeserializeImages(item.ImagesJson).OrderBy(x => x.DisplayOrder))
            {
                masterProduct.AddImage(image.Url, image.AltText, image.DisplayOrder, image.IsPrimary);
            }

            _context.MasterProducts.Add(masterProduct);
            await _context.SaveChangesAsync(cancellationToken);

            if (masterProduct.VariantGroupId == Guid.Empty)
            {
                masterProduct.ChangeVariantGroup(masterProduct.Id);
                await _context.SaveChangesAsync(cancellationToken);
            }

            item.MarkSucceeded(masterProduct.Id);
        }
        catch (DbUpdateException)
        {
            DetachIfPossible(masterProduct);
            item.MarkFailed("Product conflicts with an existing slug or barcode.");
        }
        catch (Exception ex)
        {
            DetachIfPossible(masterProduct);
            item.MarkFailed(ex.Message);
        }
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

    private void DetachIfPossible(object? entity)
    {
        if (entity is not null && _context is DbContext dbContext)
        {
            dbContext.Entry(entity).State = EntityState.Detached;
        }
    }
}
