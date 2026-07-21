using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Immutable finance audit record proving that a payout was transferred outside
/// the platform and then confirmed by an administrator.
/// </summary>
public sealed class PayoutManualConfirmation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PayoutId { get; private set; }
    public string TransferReference { get; private set; } = null!;
    public string ProofUrl { get; private set; } = null!;
    public Guid ConfirmedByUserId { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; } = DateTime.UtcNow;

    public Payout Payout { get; private set; } = null!;

    private PayoutManualConfirmation() { }

    public PayoutManualConfirmation(
        Guid payoutId,
        string transferReference,
        string proofUrl,
        Guid confirmedByUserId)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "Payout is required for manual confirmation.");
        }

        if (string.IsNullOrWhiteSpace(transferReference))
        {
            throw new BusinessRuleException("TRANSFER_REFERENCE_REQUIRED", "Transfer reference is required for manual payout confirmation.");
        }

        if (string.IsNullOrWhiteSpace(proofUrl))
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Transfer proof is required for manual payout confirmation.");
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        PayoutId = payoutId;
        TransferReference = transferReference.Trim();
        ProofUrl = proofUrl.Trim();
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = DateTime.UtcNow;
    }
}
