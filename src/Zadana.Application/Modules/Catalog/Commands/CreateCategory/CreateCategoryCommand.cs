using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name,
    string Slug,
    string? ImageUrl,
    int SortOrder,
    Guid? ParentId) : MediatR.IRequest<Guid>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);
    }
}
