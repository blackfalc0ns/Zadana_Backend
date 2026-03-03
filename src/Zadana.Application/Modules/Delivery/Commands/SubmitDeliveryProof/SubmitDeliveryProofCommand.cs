using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Delivery.Commands.SubmitDeliveryProof;

public record SubmitDeliveryProofCommand(
    Guid AssignmentId,
    string ProofType,
    string? ImageUrl,
    string? OtpCode,
    string? RecipientName,
    string? Note) : MediatR.IRequest<Guid>;

public class SubmitDeliveryProofCommandValidator : AbstractValidator<SubmitDeliveryProofCommand>
{
    public SubmitDeliveryProofCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.AssignmentId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.ProofType)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.OtpCode)
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.RecipientName)
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage(x => localizer["MaxLength"]);
            
        // Complex validation logic depending on ProofType could also be added here
        RuleFor(x => x.OtpCode)
            .NotEmpty().When(x => x.ProofType == "OTP")
            .WithMessage(x => localizer["RequiredField"]);
            
        RuleFor(x => x.ImageUrl)
            .NotEmpty().When(x => x.ProofType == "Image")
            .WithMessage(x => localizer["RequiredField"]);
    }
}
