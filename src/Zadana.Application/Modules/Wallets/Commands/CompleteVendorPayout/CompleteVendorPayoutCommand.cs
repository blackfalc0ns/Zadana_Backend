using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Wallets.Commands.CompleteVendorPayout;

public record CompleteVendorPayoutCommand(
    Guid VendorId,
    Guid PayoutId,
    string? TransferReference,
    string? ProofUrl) : IRequest<Guid>;

public class CompleteVendorPayoutCommandValidator : AbstractValidator<CompleteVendorPayoutCommand>
{
    public CompleteVendorPayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PayoutId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.TransferReference)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200)
            .WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.ProofUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(2000)
            .WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.ProofUrl)
            .Must(BeAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ProofUrl), ApplyConditionTo.CurrentValidator)
            .WithMessage("Proof URL must be an absolute HTTP(S) URL.")
            .WithErrorCode("PAYOUT_PROOF_URL_INVALID");
    }

    private static bool BeAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var proofUri) &&
        (proofUri.Scheme == Uri.UriSchemeHttp || proofUri.Scheme == Uri.UriSchemeHttps);
}

public class CompleteVendorPayoutCommandHandler : IRequestHandler<CompleteVendorPayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly PayoutOrchestrator _payoutOrchestrator;
    private readonly ICurrentUserService _currentUserService;

    public CompleteVendorPayoutCommandHandler(
        IApplicationDbContext context,
        PayoutOrchestrator payoutOrchestrator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _payoutOrchestrator = payoutOrchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CompleteVendorPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(
                item => item.Id == request.PayoutId && item.Settlement.VendorId == request.VendorId,
                cancellationToken);

        if (payout is null)
        {
            throw new InvalidOperationException("Vendor payout was not found.");
        }

        if (payout.Status == PayoutStatus.Paid)
        {
            return payout.Id;
        }

        if (string.IsNullOrWhiteSpace(request.TransferReference))
        {
            throw new BusinessRuleException("TRANSFER_REFERENCE_REQUIRED", "Transfer reference is required for manual vendor payout completion.");
        }

        if (string.IsNullOrWhiteSpace(request.ProofUrl))
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Transfer proof is required for manual vendor payout completion.");
        }

        if (!Uri.TryCreate(request.ProofUrl.Trim(), UriKind.Absolute, out var proofUri) ||
            (proofUri.Scheme != Uri.UriSchemeHttp && proofUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessRuleException("PAYOUT_PROOF_URL_INVALID", "Proof URL must be an absolute HTTP(S) URL.");
        }

        var confirmedByUserId = _currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        if (payout.Status is PayoutStatus.Cancelled or PayoutStatus.Failed)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", $"Cannot complete payout from status {payout.Status}.");
        }

        await _payoutOrchestrator.ConfirmManualAsync(
            payout.Id,
            request.TransferReference.Trim(),
            request.ProofUrl.Trim(),
            confirmedByUserId,
            cancellationToken: cancellationToken);

        return payout.Id;
    }
}
