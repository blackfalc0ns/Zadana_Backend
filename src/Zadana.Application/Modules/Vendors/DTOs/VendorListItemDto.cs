namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorListItemDto(
    Guid Id,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string Status,
    string OwnerName,
    string ContactPhone,
    DateTime CreatedAtUtc);
