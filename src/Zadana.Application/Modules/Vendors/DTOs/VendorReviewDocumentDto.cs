namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorReviewDocumentDto(
    string Id,
    string Type,
    string TitleKey,
    string DescriptionKey,
    string Icon,
    string Status,
    string StatusLabelKey,
    string IconBgClass,
    bool IsRequired,
    bool IsUploaded,
    string PreviewKind,
    string? FileUrl,
    string ReviewDecision,
    string? RejectionReason,
    DateTime? ReviewedAtUtc,
    string? ReviewedByName);
