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
    private static readonly string[] ImageProofTypes = ["image", "photo"];
    private static readonly string[] OtpProofTypes = ["otp"];

    public SubmitDeliveryProofCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.AssignmentId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.ProofType)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .Must(value =>
            {
                var normalized = value?.Trim().ToLowerInvariant();
                return normalized is not null && (ImageProofTypes.Contains(normalized) || OtpProofTypes.Contains(normalized));
            })
            .WithMessage("Proof type must be Image, Photo, or OTP")
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.OtpCode)
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.RecipientName)
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage(x => localizer["MaxLength"]);
            
        RuleFor(x => x.OtpCode)
            .NotEmpty().When(x => IsOtpProof(x.ProofType))
            .WithMessage(x => localizer["RequiredField"]);
            
        RuleFor(x => x.ImageUrl)
            .NotEmpty().When(x => IsImageProof(x.ProofType))
            .WithMessage(x => localizer["RequiredField"]);
    }

    private static bool IsImageProof(string? proofType) =>
        ImageProofTypes.Contains(proofType?.Trim().ToLowerInvariant() ?? string.Empty);

    private static bool IsOtpProof(string? proofType) =>
        OtpProofTypes.Contains(proofType?.Trim().ToLowerInvariant() ?? string.Empty);
}
