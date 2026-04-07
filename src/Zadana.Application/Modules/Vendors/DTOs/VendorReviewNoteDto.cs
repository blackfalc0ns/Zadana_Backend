namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorReviewNoteDto(
    string Id,
    string AuthorName,
    string RoleLabel,
    DateTime CreatedAtUtc,
    string? Message,
    string? MessageKey,
    string Tone,
    bool IsSystem);
