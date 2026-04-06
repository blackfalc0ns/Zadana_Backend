namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorListItemDto(
    Guid Id,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string Status,
    string OwnerName,
    string ContactPhone,
    DateTime CreatedAtUtc,
    string ContactEmail = "",
    decimal? CommissionRate = null,
    string? City = null,
    string? Region = null,
    string? AccountStatus = null,
    bool IsLoginLocked = false,
    DateTime? LockedAtUtc = null,
    DateTime? ArchivedAtUtc = null);
