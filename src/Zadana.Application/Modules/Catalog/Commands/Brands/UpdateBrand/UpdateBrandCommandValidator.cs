using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator(IStringLocalizer<SharedResource> localizer, IApplicationDbContext context)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(localizer["RequiredField"].Value).WithName("Id");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(150).WithMessage(localizer["MaxLength"].Value)
            .WithName("NameAr");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(150).WithMessage(localizer["MaxLength"].Value)
            .WithName("NameEn");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .Must(NotBeBrowserBlobUrl).WithMessage("Browser preview blob URLs cannot be saved. Upload the image first and save the returned cloud URL.")
            .WithName("LogoUrl");

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .Must(NotBeBrowserBlobUrl).WithMessage("Browser preview blob URLs cannot be saved. Upload the image first and save the returned cloud URL.")
            .WithName("CoverImageUrl");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MustAsync(async (categoryId, cancellationToken) =>
            {
                var category = await context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken);

                return category is { ParentCategoryId: not null };
            })
            .WithMessage(localizer["BrandMustBeLinkedToSubcategory"].Value)
            .WithName("CategoryId");

        RuleFor(x => ResolveCategoryIds(x.CategoryId, x.CategoryIds))
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MustAsync(async (categoryIds, cancellationToken) =>
            {
                var uniqueIds = categoryIds.Distinct().ToArray();
                var validCount = await context.Categories
                    .AsNoTracking()
                    .CountAsync(item => uniqueIds.Contains(item.Id) && item.ParentCategoryId != null, cancellationToken);

                return validCount == uniqueIds.Length;
            })
            .WithMessage(localizer["BrandMustBeLinkedToSubcategory"].Value)
            .WithName("CategoryIds");
    }

    private static bool NotBeBrowserBlobUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith("blob:", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Guid> ResolveCategoryIds(Guid categoryId, IReadOnlyList<Guid>? categoryIds)
    {
        return categoryIds is { Count: > 0 }
            ? categoryIds.Where(id => id != Guid.Empty).Distinct().ToArray()
            : [categoryId];
    }
}
