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

public record FeaturedProductSelectionSettingsDto(
    string SelectionMode,
    int TargetCount,
    int MinSalesCount,
    int MinStoreCount,
    bool RequireDiscount,
    bool ExcludeProductsAlreadyInSpecialOffers);

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

public record CouponVendorAdminDto(
    Guid VendorId,
    string VendorNameAr,
    string VendorNameEn);

public record CouponAdminDto(
    Guid Id,
    string Code,
    string Title,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    int? UsageLimit,
    int? PerUserLimit,
    bool IsActive,
    int AssignedVendorsCount,
    IReadOnlyList<CouponVendorAdminDto> ApplicableVendors,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
