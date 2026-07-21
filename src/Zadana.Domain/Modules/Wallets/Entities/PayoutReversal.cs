using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Immutable evidence that funds from a previously paid payout were returned to
/// the platform bank account. Reversals are recorded separately from the
/// original confirmation so the original payout evidence is never overwritten.
/// </summary>
public sealed class PayoutReversal
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PayoutId { get; private set; }
    public string ReturnReference { get; private set; } = null!;
    /// <summary>
    /// The protected attachment used for new return records. A nullable legacy
    /// URL remains only so historical audit rows can still be retained without
    /// exposing the URL through DTOs.
    /// </summary>
    public Guid? ProofAttachmentId { get; private set; }
    public string? LegacyProofUrl { get; private set; }
    public string? Reason { get; private set; }
    public Guid ConfirmedByUserId { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; } = DateTime.UtcNow;

    public Payout Payout { get; private set; } = null!;
    public PayoutProofAttachment? ProofAttachment { get; private set; }

    private PayoutReversal() { }

    public PayoutReversal(
        Guid payoutId,
        string returnReference,
        Guid proofAttachmentId,
        Guid confirmedByUserId,
        string? reason = null)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "Payout is required for a return confirmation.");
        }

        if (string.IsNullOrWhiteSpace(returnReference))
        {
            throw new BusinessRuleException("RETURN_REFERENCE_REQUIRED", "Bank return reference is required.");
        }

        if (proofAttachmentId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Return proof is required.");
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        PayoutId = payoutId;
        ReturnReference = returnReference.Trim();
        ProofAttachmentId = proofAttachmentId;
        LegacyProofUrl = null;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = DateTime.UtcNow;
    }
}
