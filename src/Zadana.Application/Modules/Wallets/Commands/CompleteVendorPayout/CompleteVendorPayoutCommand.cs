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
    Guid? ProofAttachmentId) : IRequest<Guid>;

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
        RuleFor(x => x.ProofAttachmentId)
            .NotNull().WithMessage(x => localizer["RequiredField"])
            .Must(value => value.HasValue && value.Value != Guid.Empty)
            .WithErrorCode("PAYOUT_PROOF_REQUIRED")
            .WithMessage("A protected payout proof attachment is required.");
    }
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

        if (!request.ProofAttachmentId.HasValue || request.ProofAttachmentId.Value == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Transfer proof is required for manual vendor payout completion.");
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
            request.ProofAttachmentId.Value,
            confirmedByUserId,
            cancellationToken: cancellationToken);

        return payout.Id;
    }
}
