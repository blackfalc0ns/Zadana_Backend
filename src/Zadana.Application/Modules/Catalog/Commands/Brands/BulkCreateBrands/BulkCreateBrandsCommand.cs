using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.BulkCreateBrands;

public record BulkCreateBrandItemInput(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid CategoryId,
    bool IsActive);

public record BulkCreateBrandsCommand(
    Guid AdminUserId,
    string IdempotencyKey,
    IReadOnlyList<BulkCreateBrandItemInput> Items) : MediatR.IRequest<AdminBrandBulkOperationDto>;

public class BulkCreateBrandsCommandValidator : AbstractValidator<BulkCreateBrandsCommand>
{
    public BulkCreateBrandsCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage(x => localizer["RequiredField"])
                .MaximumLength(150).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.NameEn)
                .NotEmpty().WithMessage(x => localizer["RequiredField"])
                .MaximumLength(150).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.LogoUrl)
                .MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.LogoUrl)).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage(x => localizer["RequiredField"]);
        });
    }
}
