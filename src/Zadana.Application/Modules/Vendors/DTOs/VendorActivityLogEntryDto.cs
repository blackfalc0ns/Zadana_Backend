namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorActivityLogEntryDto(
    string Id,
    string Type,
    string Severity,
    string ActorName,
    string RoleLabel,
    DateTime CreatedAtUtc,
    string Message,
    bool IsSystem);
