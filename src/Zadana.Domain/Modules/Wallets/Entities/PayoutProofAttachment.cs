using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Encrypted, immutable evidence uploaded for a payout. The database contains
/// only Data Protection-protected bytes; it never stores a public storage URL.
/// </summary>
public sealed class PayoutProofAttachment : BaseEntity
{
    public Guid PayoutId { get; private set; }
    public PayoutProofKind Kind { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long ContentLength { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public byte[] ProtectedContent { get; private set; } = [];
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; } = DateTime.UtcNow;
    public Guid? FinalizedByUserId { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Payout Payout { get; private set; } = null!;

    public bool IsFinalized => FinalizedAtUtc.HasValue;

    private PayoutProofAttachment() { }

    public PayoutProofAttachment(
        Guid payoutId,
        PayoutProofKind kind,
        string fileName,
        string contentType,
        long contentLength,
        string sha256,
        byte[] protectedContent,
        Guid uploadedByUserId)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "A payout is required for a proof attachment.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BusinessRuleException("PAYOUT_PROOF_FILE_NAME_REQUIRED", "A proof file name is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new BusinessRuleException("PAYOUT_PROOF_CONTENT_TYPE_REQUIRED", "A proof content type is required.");
        }

        if (contentLength <= 0)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_EMPTY", "A proof attachment cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sha256) || sha256.Trim().Length != 64)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_HASH_INVALID", "A SHA-256 proof hash is required.");
        }

        if (protectedContent is null || protectedContent.Length == 0)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_CONTENT_REQUIRED", "Protected proof content is required.");
        }

        if (uploadedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_UPLOADER_REQUIRED", "The proof uploader is required.");
        }

        PayoutId = payoutId;
        Kind = kind;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        ContentLength = contentLength;
        Sha256 = sha256.Trim().ToUpperInvariant();
        ProtectedContent = protectedContent;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = DateTime.UtcNow;
    }

    public void FinalizeForUse(Guid finalizedByUserId)
    {
        if (finalizedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        if (IsFinalized)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_ALREADY_FINALIZED", "The proof attachment has already been finalized.");
        }

        FinalizedByUserId = finalizedByUserId;
        FinalizedAtUtc = DateTime.UtcNow;
    }
}
