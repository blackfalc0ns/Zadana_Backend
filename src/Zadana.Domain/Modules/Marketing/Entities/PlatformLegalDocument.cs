using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

/// <summary>
/// Admin-managed legal document (terms or privacy) for a platform audience.
/// </summary>
public sealed class PlatformLegalDocument : BaseEntity
{
    public PlatformLegalDocumentType DocumentType { get; private set; }
    public string ContentAr { get; private set; } = string.Empty;
    public string ContentEn { get; private set; } = string.Empty;
    public string Version { get; private set; } = "1.0";
    public DateTime EffectiveAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private PlatformLegalDocument()
    {
    }

    public PlatformLegalDocument(
        PlatformLegalDocumentType documentType,
        string? contentAr = null,
        string? contentEn = null,
        string? version = null,
        DateTime? effectiveAtUtc = null,
        Guid? updatedByUserId = null)
    {
        DocumentType = documentType;
        ContentAr = NormalizeContent(contentAr);
        ContentEn = NormalizeContent(contentEn);
        Version = NormalizeVersion(version);
        EffectiveAtUtc = effectiveAtUtc?.ToUniversalTime() ?? DateTime.UtcNow.Date;
        UpdatedByUserId = updatedByUserId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string? contentAr,
        string? contentEn,
        string? version,
        DateTime? effectiveAtUtc,
        Guid? updatedByUserId)
    {
        ContentAr = NormalizeContent(contentAr);
        ContentEn = NormalizeContent(contentEn);
        Version = NormalizeVersion(version);
        if (effectiveAtUtc.HasValue)
        {
            EffectiveAtUtc = effectiveAtUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(effectiveAtUtc.Value, DateTimeKind.Utc)
                : effectiveAtUtc.Value.ToUniversalTime();
        }

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeContent(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeVersion(string? value)
    {
        var version = string.IsNullOrWhiteSpace(value) ? "1.0" : value.Trim();
        if (version.Length > 32)
        {
            throw new BusinessRuleException("LEGAL_VERSION_INVALID", "Version must be 32 characters or fewer.");
        }

        return version;
    }
}
