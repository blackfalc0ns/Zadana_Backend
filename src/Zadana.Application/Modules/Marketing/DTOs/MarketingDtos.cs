namespace Zadana.Application.Modules.Marketing.DTOs;

public record HomeBannerAdminDto(
    Guid Id,
    string TagAr,
    string TagEn,
    string TitleAr,
    string TitleEn,
    string? SubtitleAr,
    string? SubtitleEn,
    string? ActionLabelAr,
    string? ActionLabelEn,
    string ImageUrl,
    int DisplayOrder,
    bool IsActive,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record FeaturedProductPlacementDto(
    Guid Id,
    string PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId,
    string? DisplayNameAr,
    string? DisplayNameEn,
    int DisplayOrder,
    bool IsActive,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record HomeSectionAdminDto(
    Guid Id,
    Guid CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    string Theme,
    string ThemeLabelAr,
    string ThemeLabelEn,
    int DisplayOrder,
    int ProductsTake,
    bool IsActive,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record HomeSectionThemeOptionDto(
    string Key,
    string LabelAr,
    string LabelEn);

public record HomeContentSectionSettingDto(
    string SectionType,
    bool IsEnabled);
