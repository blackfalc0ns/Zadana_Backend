using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator(IStringLocalizer<SharedResource> localizer, IApplicationDbContext context)
    {
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
            .WithName("LogoUrl");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MustAsync(async (categoryId, cancellationToken) =>
            {
                var category = await context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken);

                if (category == null)
                {
                    return false;
                }

                return !await context.Categories
                    .AsNoTracking()
                    .AnyAsync(item => item.ParentCategoryId == categoryId, cancellationToken);
            })
            .WithMessage("CategoryId must reference a valid leaf category.")
            .WithName("CategoryId");
    }
}
