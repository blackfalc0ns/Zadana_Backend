using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class PayoutAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PayoutId { get; private set; }
    public PayoutAttemptType AttemptType { get; private set; }
    public PayoutStatus Status { get; private set; }
    public string ProviderName { get; private set; } = "Paymob";
    public string? ProviderTransferId { get; private set; }
    public string? TransferReference { get; private set; }
    public string? FailureReason { get; private set; }
    public string? RawPayload { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public Payout Payout { get; private set; } = null!;

    private PayoutAttempt() { }

    public PayoutAttempt(
        Guid payoutId,
        PayoutAttemptType attemptType,
        PayoutStatus status,
        string providerName = "Paymob",
        string? providerTransferId = null,
        string? transferReference = null,
        string? failureReason = null,
        string? rawPayload = null)
    {
        PayoutId = payoutId;
        AttemptType = attemptType;
        Status = status;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? "Paymob" : providerName.Trim();
        ProviderTransferId = string.IsNullOrWhiteSpace(providerTransferId) ? null : providerTransferId.Trim();
        TransferReference = string.IsNullOrWhiteSpace(transferReference) ? null : transferReference.Trim();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        RawPayload = string.IsNullOrWhiteSpace(rawPayload) ? null : rawPayload.Trim();
    }
}
