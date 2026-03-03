using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateBrand;

public record CreateBrandCommand(
    string Name,
    string Slug,
    string? LogoUrl) : MediatR.IRequest<Guid>;

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);
    }
}
