using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Common;

internal static partial class CatalogRequestWorkflowSupport
{
    public static string NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    public static bool HasSameLocalizedName(
        string currentNameAr,
        string currentNameEn,
        string requestedNameAr,
        string requestedNameEn) =>
        string.Equals(NormalizeName(currentNameAr), NormalizeName(requestedNameAr), StringComparison.Ordinal) &&
        string.Equals(NormalizeName(currentNameEn), NormalizeName(requestedNameEn), StringComparison.Ordinal);

    public static async Task<string> GenerateUniqueMasterProductSlugAsync(
        IApplicationDbContext context,
        string? englishName,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugCleanupRegex()
            .Replace((englishName ?? string.Empty).Trim().ToLowerInvariant(), "-")
            .Trim('-');

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "product";
        }

        baseSlug = DuplicateDashRegex().Replace(baseSlug, "-");

        var candidate = baseSlug;
        var suffix = 2;

        while (await context.MasterProducts.AnyCompatAsync(
                   item => item.Slug == candidate,
                   cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    public static async Task<Brand?> FindMatchingBrandAsync(
        IApplicationDbContext context,
        string nameAr,
        string nameEn,
        CancellationToken cancellationToken)
    {
        var normalizedNameAr = NormalizeName(nameAr);
        var normalizedNameEn = NormalizeName(nameEn);

        return await context.Brands
            .Include(item => item.BrandCategories)
            .FirstOrDefaultAsync(
                item => item.NameAr.ToUpper() == normalizedNameAr &&
                        item.NameEn.ToUpper() == normalizedNameEn,
                cancellationToken);
    }

    public static bool BrandMatchesCategory(Brand brand, Guid categoryId) =>
        BrandMatchesCategory(brand, categoryId, parentById: null);

    public static bool BrandMatchesCategory(
        Brand brand,
        Guid categoryId,
        IReadOnlyDictionary<Guid, Guid?>? parentById)
    {
        var linkedCategoryIds = new HashSet<Guid>();
        if (brand.CategoryId.HasValue)
        {
            linkedCategoryIds.Add(brand.CategoryId.Value);
        }

        foreach (var link in brand.BrandCategories)
        {
            linkedCategoryIds.Add(link.CategoryId);
        }

        if (linkedCategoryIds.Count == 0)
        {
            return true;
        }

        if (linkedCategoryIds.Contains(categoryId))
        {
            return true;
        }

        if (parentById is null || parentById.Count == 0)
        {
            return false;
        }

        var currentId = categoryId;
        var guard = 0;
        while (guard++ < 16)
        {
            if (!parentById.TryGetValue(currentId, out var parentId) || !parentId.HasValue)
            {
                break;
            }

            currentId = parentId.Value;
            if (linkedCategoryIds.Contains(currentId))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> BrandMatchesCategoryAsync(
        IApplicationDbContext context,
        Brand brand,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var linkedCategoryIds = new HashSet<Guid>();
        if (brand.CategoryId.HasValue)
        {
            linkedCategoryIds.Add(brand.CategoryId.Value);
        }

        foreach (var link in brand.BrandCategories)
        {
            linkedCategoryIds.Add(link.CategoryId);
        }

        if (linkedCategoryIds.Count == 0 || linkedCategoryIds.Contains(categoryId))
        {
            return linkedCategoryIds.Count == 0 || linkedCategoryIds.Contains(categoryId);
        }

        var currentId = (Guid?)categoryId;
        var guard = 0;
        while (currentId.HasValue && guard++ < 16)
        {
            if (linkedCategoryIds.Contains(currentId.Value))
            {
                return true;
            }

            currentId = await context.Categories
                .AsNoTracking()
                .Where(item => item.Id == currentId.Value)
                .Select(item => item.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public static async Task EnsureBrandRequestCanBeSubmittedAsync(
        IApplicationDbContext context,
        Guid vendorId,
        Guid categoryId,
        string nameAr,
        string nameEn,
        CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), categoryId);

        if (category.ParentCategoryId is null)
        {
            throw new BusinessRuleException("BRAND_CATEGORY_MUST_BE_NESTED", "The selected category is not valid for brand requests.");
        }

        var normalizedNameAr = NormalizeName(nameAr);
        var normalizedNameEn = NormalizeName(nameEn);

        var duplicatePendingRequestExists = await context.BrandRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.VendorId == vendorId &&
                        item.Status == ApprovalStatus.Pending &&
                        item.CategoryId == categoryId &&
                        item.NameAr.ToUpper() == normalizedNameAr &&
                        item.NameEn.ToUpper() == normalizedNameEn,
                cancellationToken);

        if (duplicatePendingRequestExists)
        {
            throw new BusinessRuleException(
                "BRAND_REQUEST_ALREADY_PENDING",
                "A matching brand request is already pending review.");
        }
    }

    public static async Task<string> ValidateAndResolveCategoryTargetLevelAsync(
        IApplicationDbContext context,
        Guid vendorId,
        string nameAr,
        string nameEn,
        string targetLevel,
        Guid? parentCategoryId,
        CancellationToken cancellationToken)
    {
        if (!CategoryHierarchyRules.TryParseTargetLevel(targetLevel, out var parsedTargetLevel))
        {
            throw new BusinessRuleException("INVALID_CATEGORY_TARGET_LEVEL", "Invalid category target level.");
        }

        if (!CategoryHierarchyRules.IsValidLevel(parsedTargetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Category requests cannot exceed the fourth level.");
        }

        if (!CategoryHierarchyRules.IsRequestTargetLevel(parsedTargetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Only category and sub-category requests are supported.");
        }

        var categories = await context.Categories
            .AsNoTracking()
            .Select(category => new CategoryNode(category.Id, category.ParentCategoryId))
            .ToListAsync(cancellationToken);

        var categoryLookup = categories.ToDictionary(category => category.Id);

        if (parentCategoryId.HasValue && !categoryLookup.ContainsKey(parentCategoryId.Value))
        {
            throw new NotFoundException(nameof(Category), parentCategoryId.Value);
        }

        if (!parentCategoryId.HasValue)
        {
            throw new BusinessRuleException("CATEGORY_PARENT_REQUIRED", "This category level requires a parent category.");
        }

        var parentLevel = ResolveLevel(parentCategoryId.Value, categoryLookup);

        if (!CategoryHierarchyRules.IsAllowedParentLevel(parsedTargetLevel, parentLevel))
        {
            throw new BusinessRuleException("INVALID_CATEGORY_PARENT_LEVEL", "The selected parent category does not match the requested level.");
        }

        var normalizedNameAr = NormalizeName(nameAr);
        var normalizedNameEn = NormalizeName(nameEn);
        var targetLevelKey = CategoryHierarchyRules.ToKey(parsedTargetLevel);

        var duplicateCategoryExists = await context.Categories
            .AsNoTracking()
            .AnyAsync(
                item => item.ParentCategoryId == parentCategoryId &&
                        item.NameAr.ToUpper() == normalizedNameAr &&
                        item.NameEn.ToUpper() == normalizedNameEn,
                cancellationToken);

        if (duplicateCategoryExists)
        {
            throw new BusinessRuleException(
                "CATEGORY_ALREADY_EXISTS",
                "A category with the same name already exists under the selected parent.");
        }

        var duplicatePendingRequestExists = await context.CategoryRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.VendorId == vendorId &&
                        item.Status == ApprovalStatus.Pending &&
                        item.ParentCategoryId == parentCategoryId &&
                        item.TargetLevel == targetLevelKey &&
                        item.NameAr.ToUpper() == normalizedNameAr &&
                        item.NameEn.ToUpper() == normalizedNameEn,
                cancellationToken);

        if (duplicatePendingRequestExists)
        {
            throw new BusinessRuleException(
                "CATEGORY_REQUEST_ALREADY_PENDING",
                "A matching category request is already pending review.");
        }

        return targetLevelKey;
    }

    private static int ResolveLevel(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> lookup)
    {
        var level = 0;
        var currentId = categoryId;

        while (lookup.TryGetValue(currentId, out var current) && current.ParentCategoryId.HasValue)
        {
            level++;

            if (level > CategoryHierarchyRules.MaxLevel)
            {
                throw new BusinessRuleException("CATEGORY_DEPTH_EXCEEDED", "Categories deeper than the supported hierarchy are not allowed.");
            }

            currentId = current.ParentCategoryId.Value;
        }

        return level;
    }

    private sealed record CategoryNode(Guid Id, Guid? ParentCategoryId);

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex SlugCleanupRegex();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex DuplicateDashRegex();
}
