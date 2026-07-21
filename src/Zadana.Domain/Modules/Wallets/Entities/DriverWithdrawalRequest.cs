using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class DriverWithdrawalRequest : BaseEntity
{
    public Guid DriverId { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid DriverPayoutMethodId { get; private set; }
    public Guid? PayoutId { get; private set; }
    public decimal Amount { get; private set; }
    public string? RequestIdempotencyKey { get; private set; }
    public PayoutScheduleDay? RequestedPayoutDay { get; private set; }
    public string? DestinationSnapshot { get; private set; }
    public DriverWithdrawalStatus Status { get; private set; }
    public string? TransferReference { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public Wallet Wallet { get; private set; } = null!;
    public DriverPayoutMethod DriverPayoutMethod { get; private set; } = null!;
    public Payout? Payout { get; private set; }

    private DriverWithdrawalRequest() { }

    public DriverWithdrawalRequest(
        Guid driverId,
        Guid walletId,
        Guid driverPayoutMethodId,
        decimal amount,
        string? requestIdempotencyKey = null,
        PayoutScheduleDay? requestedPayoutDay = null,
        string? destinationSnapshot = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_WITHDRAWAL_AMOUNT", "Withdrawal amount must be greater than zero.");
        }

        DriverId = driverId;
        WalletId = walletId;
        DriverPayoutMethodId = driverPayoutMethodId;
        Amount = amount;
        RequestIdempotencyKey = NormalizeIdempotencyKey(requestIdempotencyKey);
        RequestedPayoutDay = requestedPayoutDay;
        DestinationSnapshot = string.IsNullOrWhiteSpace(destinationSnapshot)
            ? null
            : destinationSnapshot.Trim();
        Status = DriverWithdrawalStatus.Pending;
    }

    public void RecordApproval(Guid reviewedByUserId)
    {
        EnsureReviewer(reviewedByUserId);
        ReviewedByUserId ??= reviewedByUserId;
        ReviewedAtUtc ??= DateTime.UtcNow;
    }

    public void RecordRejection(Guid reviewedByUserId, string? reason)
    {
        EnsureReviewer(reviewedByUserId);
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = DateTime.UtcNow;
        MarkFailed(reason);
    }

    public void LinkPayout(Guid payoutId)
    {
        if (PayoutId.HasValue && PayoutId.Value != payoutId)
        {
            throw new BusinessRuleException("WITHDRAWAL_PAYOUT_ALREADY_LINKED", "Withdrawal is already linked to another payout.");
        }

        PayoutId = payoutId;
    }

    public void MarkProcessing()
    {
        Status = DriverWithdrawalStatus.Processing;
    }

    public void MarkPaid(string? transferReference)
    {
        Status = DriverWithdrawalStatus.Paid;
        TransferReference = string.IsNullOrWhiteSpace(transferReference) ? null : transferReference.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkReturned(string? reason)
    {
        if (Status != DriverWithdrawalStatus.Paid)
        {
            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_RETURN_INVALID_STATUS",
                "Only a paid driver withdrawal can be marked as returned.");
        }

        Status = DriverWithdrawalStatus.Returned;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Bank transfer returned." : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string? reason)
    {
        Status = DriverWithdrawalStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        Status = DriverWithdrawalStatus.Cancelled;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 160)
        {
            throw new BusinessRuleException(
                "WITHDRAWAL_IDEMPOTENCY_KEY_TOO_LONG",
                "Withdrawal idempotency key cannot exceed 160 characters.");
        }

        return normalized;
    }

    private static void EnsureReviewer(Guid reviewedByUserId)
    {
        if (reviewedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "WITHDRAWAL_REVIEWER_REQUIRED",
                "An authenticated finance administrator is required to review a withdrawal.");
        }
    }
}
