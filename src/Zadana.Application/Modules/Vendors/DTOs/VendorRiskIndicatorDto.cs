namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorRiskIndicatorDto(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string Severity, // "high", "medium", "low"
    string SeverityLabelKey,
    string Icon,
    string? TitleAr = null,
    string? TitleEn = null,
    string? DescriptionAr = null,
    string? DescriptionEn = null);
