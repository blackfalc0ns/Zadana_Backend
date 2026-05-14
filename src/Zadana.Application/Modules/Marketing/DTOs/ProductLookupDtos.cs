namespace Zadana.Application.Modules.Marketing.DTOs;

public record MasterProductLookupDto(
    Guid Id,
    string NameAr,
    string NameEn);

public record VendorProductLookupDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string VendorNameAr,
    string VendorNameEn);
