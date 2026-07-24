using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Persistence.Encryption;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Services;

/// <summary>
/// Stores payout evidence inside the finance database after protecting the raw
/// bytes with a stable AES-GCM key (plus legacy Data Protection fallback on
/// read). This is intentionally separate from generic media storage: finance
/// proofs must not receive a public URL or be mutable after upload.
/// </summary>
public sealed class PayoutProofAttachmentService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxPdfBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, ProofFileRule> AllowedFileTypes =
        new Dictionary<string, ProofFileRule>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new("image/jpeg", MaxImageBytes),
            [".jpeg"] = new("image/jpeg", MaxImageBytes),
            [".png"] = new("image/png", MaxImageBytes),
            [".webp"] = new("image/webp", MaxImageBytes),
            [".pdf"] = new("application/pdf", MaxPdfBytes)
        };

    private readonly IApplicationDbContext _context;
    private readonly PayoutProofContentProtector _protector;

    public PayoutProofAttachmentService(
        IApplicationDbContext context,
        PayoutProofContentProtector protector)
    {
        _context = context;
        _protector = protector;
    }

    public async Task<PayoutProofAttachment> UploadAsync(
        Guid payoutId,
        PayoutProofKind kind,
        IFormFile? file,
        Guid uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BadRequestException("PAYOUT_REQUIRED", "Payout is required for proof upload.");
        }

        if (uploadedByUserId == Guid.Empty)
        {
            throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        }

        if (file is null || file.Length <= 0)
        {
            throw new BadRequestException("PAYOUT_PROOF_REQUIRED", "A non-empty payout proof file is required.");
        }

        var fileName = NormalizeFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedFileTypes.TryGetValue(extension, out var rule))
        {
            throw new BadRequestException(
                "INVALID_FILE_EXTENSION",
                "Payout proof must be a PDF, JPEG, PNG, or WebP file.");
        }

        var declaredContentType = NormalizeContentType(file.ContentType);
        if (!string.Equals(declaredContentType, rule.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "INVALID_FILE_CONTENT_TYPE",
                "The payout proof content type does not match its file extension.");
        }

        if (file.Length > rule.MaxBytes)
        {
            throw new BadRequestException(
                "FILE_TOO_LARGE",
                $"The payout proof exceeds the allowed {rule.MaxBytes / (1024 * 1024)} MB limit.");
        }

        var content = await ReadContentAsync(file, rule.MaxBytes, cancellationToken);
        EnsureFileSignature(content, extension);

        var hash = Convert.ToHexString(SHA256.HashData(content));
        var existing = await _context.PayoutProofAttachments
            .FirstOrDefaultAsync(
                item => item.PayoutId == payoutId && item.Kind == kind && item.Sha256 == hash,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // Hash-based retries must remain idempotent after the original request
        // has advanced the payout to Paid/Confirmed. The state gate therefore
        // applies only when storing genuinely new evidence.
        await EnsurePayoutCanReceiveProofAsync(payoutId, kind, cancellationToken);

        var attachment = new PayoutProofAttachment(
            payoutId,
            kind,
            fileName,
            rule.ContentType,
            content.LongLength,
            hash,
            _protector.Protect(content),
            uploadedByUserId);

        _context.PayoutProofAttachments.Add(attachment);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique (payout, kind, SHA-256) index makes retries
            // idempotent even when two HTTP requests arrive together.
            Detach(attachment);
            var concurrentlyStored = await _context.PayoutProofAttachments
                .FirstOrDefaultAsync(
                    item => item.PayoutId == payoutId && item.Kind == kind && item.Sha256 == hash,
                    cancellationToken);
            if (concurrentlyStored is not null)
            {
                return concurrentlyStored;
            }

            throw;
        }

        return attachment;
    }

    public async Task<PayoutProofDownload> GetForDownloadAsync(
        Guid payoutId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _context.PayoutProofAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == attachmentId && item.PayoutId == payoutId,
                cancellationToken)
            ?? throw new NotFoundException("Payout proof attachment", attachmentId);

        byte[] content;
        try
        {
            content = _protector.Unprotect(attachment.ProtectedContent);
        }
        catch (CryptographicException exception)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_UNAVAILABLE",
                "The protected payout proof cannot be decrypted with the active application key ring.",
                exception.Message);
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(content));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(attachment.Sha256)))
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_INTEGRITY_FAILED",
                "The stored payout proof failed its integrity verification.");
        }

        return new PayoutProofDownload(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            content);
    }

    private async Task EnsurePayoutCanReceiveProofAsync(
        Guid payoutId,
        PayoutProofKind kind,
        CancellationToken cancellationToken)
    {
        var payout = await _context.Payouts
            .AsNoTracking()
            .Include(item => item.ExecutionReservation)
            .Include(item => item.Reversal)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);

        if (kind == PayoutProofKind.ManualTransfer)
        {
            var reservation = payout.ExecutionReservation;
            if (reservation?.Mode != PayoutExecutionMode.Manual ||
                reservation.Status != PayoutExecutionReservationStatus.Submitted)
            {
                throw new BusinessRuleException(
                    "PAYOUT_PROOF_MANUAL_SUBMISSION_REQUIRED",
                    "Record the manual bank submission before uploading its transfer proof.");
            }

            if (!string.IsNullOrWhiteSpace(payout.ProviderTransferId))
            {
                throw new BusinessRuleException(
                    "PAYOUT_PROVIDER_RECONCILIATION_REQUIRED",
                    "This payout already has a gateway transfer reference and must be reconciled with the provider before accepting manual transfer proof.");
            }

            // Keep this eligibility rule aligned with the manual-confirmation
            // workflow. Pending/Failed cover safely recoverable legacy records
            // whose durable manual reservation is already Submitted.
            var isEligibleStatus = payout.Status is PayoutStatus.Pending or PayoutStatus.Failed ||
                (payout.Status is PayoutStatus.Queued or PayoutStatus.Processing &&
                 string.Equals(payout.ProviderName, "Manual", StringComparison.OrdinalIgnoreCase));
            if (!isEligibleStatus)
            {
                throw new BusinessRuleException(
                    "PAYOUT_INVALID_STATUS",
                    $"Cannot upload manual transfer proof for payout in status {payout.Status}.");
            }

            return;
        }

        if (payout.Status != PayoutStatus.Paid || payout.Reversal is not null)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_RETURN_INVALID_STATUS",
                "A return proof can only be uploaded for a paid payout that has not already been reversed.");
        }
    }

    private static async Task<byte[]> ReadContentAsync(
        IFormFile file,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        // IFormFile does not expose a max-length overload on all supported ASP.NET
        // versions. The streaming guard below is the authoritative size check.
        await using var input = file.OpenReadStream();
        await using var output = new MemoryStream((int)Math.Min(file.Length, maxBytes));
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new BadRequestException(
                    "FILE_TOO_LARGE",
                    $"The payout proof exceeds the allowed {maxBytes / (1024 * 1024)} MB limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw new BadRequestException("PAYOUT_PROOF_REQUIRED", "A non-empty payout proof file is required.");
        }

        return output.ToArray();
    }

    private static string NormalizeFileName(string? value)
    {
        var fileName = Path.GetFileName(value?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length > 255 ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains('/') ||
            fileName.Contains('\\'))
        {
            throw new BadRequestException("PAYOUT_PROOF_FILE_NAME_INVALID", "The payout proof file name is invalid.");
        }

        return fileName;
    }

    private static string NormalizeContentType(string? value) =>
        value?.Split(';', 2, StringSplitOptions.TrimEntries)[0].Trim() ?? string.Empty;

    private static void EnsureFileSignature(byte[] content, string extension)
    {
        var matches = extension switch
        {
            ".jpg" or ".jpeg" => content.Length >= 3 &&
                content.AsSpan(0, 3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => content.Length >= 8 &&
                content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => content.Length >= 12 &&
                content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".pdf" => ContainsPdfHeader(content),
            _ => false
        };

        if (!matches)
        {
            throw new BadRequestException(
                "INVALID_FILE_SIGNATURE",
                "The payout proof content does not match the selected file type.");
        }
    }

    private static bool ContainsPdfHeader(byte[] content)
    {
        var header = "%PDF"u8;
        var scanLength = Math.Min(content.Length, 1024);
        for (var index = 0; index <= scanLength - header.Length; index++)
        {
            if (content.AsSpan(index, header.Length).SequenceEqual(header))
            {
                return true;
            }
        }

        return false;
    }

    private void Detach(PayoutProofAttachment attachment)
    {
        if (_context is DbContext dbContext)
        {
            dbContext.Entry(attachment).State = EntityState.Detached;
        }
    }

    private sealed record ProofFileRule(string ContentType, long MaxBytes);
}

public sealed record PayoutProofDownload(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    byte[] Content);
