using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Money set aside on a vendor/driver/platform wallet that is not available
/// for withdrawal until the hold is released, consumed, or cancelled.
/// Replaces the in-row <c>Wallet.PendingBalance</c> mechanic in section 13 of the spec.
/// </summary>
public class WalletHold : BaseEntity
{
    public WalletOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid? WalletId { get; private set; }

    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = CurrencyPolicy.OfficialCurrency;

    public WalletHoldReason Reason { get; private set; }
    public WalletHoldStatus Status { get; private set; }

    /// <summary>Optional free-form name of the entity referenced by <see cref="ReferenceId"/> (e.g. "Settlement", "DriverWithdrawalRequest").</summary>
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }

    public string IdempotencyKey { get; private set; } = null!;

    public DateTime CreatedAtUtcOnHold { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public string? FailureReason { get; private set; }
    public string? Memo { get; private set; }

    private WalletHold() { }

    public WalletHold(
        WalletOwnerType ownerType,
        Guid ownerId,
        decimal amount,
        WalletHoldReason reason,
        string idempotencyKey,
        string? currencyCode = null,
        Guid? walletId = null,
        string? referenceType = null,
        Guid? referenceId = null,
        DateTime? expiresAtUtc = null,
        string? memo = null)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_HOLD_AMOUNT", "Wallet hold amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("INVALID_IDEMPOTENCY_KEY", "Wallet hold requires an idempotency key.");
        }

        var normalizedCurrency = CurrencyPolicy.Normalize(currencyCode);
        CurrencyPolicy.EnsureOfficial(normalizedCurrency);

        OwnerType = ownerType;
        OwnerId = ownerId;
        WalletId = walletId;
        Amount = amount;
        CurrencyCode = normalizedCurrency;
        Reason = reason;
        Status = WalletHoldStatus.Active;
        ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim();
        ReferenceId = referenceId;
        IdempotencyKey = idempotencyKey.Trim();
        CreatedAtUtcOnHold = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
    }

    public bool IsActive => Status == WalletHoldStatus.Active;

    public void Release(string? reason = null)
    {
        EnsureActive();
        Status = WalletHoldStatus.Released;
        ReleasedAtUtc = DateTime.UtcNow;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? FailureReason : reason.Trim();
    }

    public void Consume()
    {
        EnsureActive();
        Status = WalletHoldStatus.Consumed;
        ConsumedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        EnsureActive();
        Status = WalletHoldStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? FailureReason : reason.Trim();
    }

    public void Expire()
    {
        EnsureActive();
        Status = WalletHoldStatus.Expired;
        ReleasedAtUtc = DateTime.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status != WalletHoldStatus.Active)
        {
            throw new BusinessRuleException(
                "WALLET_HOLD_NOT_ACTIVE",
                $"Wallet hold {Id} is in status {Status} and cannot transition.");
        }
    }
}
