namespace Zadana.Api.Modules.Marketing.Requests;

public record CreateHomeBannerRequest(
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
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc);

public record UpdateHomeBannerRequest(
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
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive);

public record CreateFeaturedPlacementRequest(
    string PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    string? Note);

public record UpdateFeaturedPlacementRequest(
    string PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive,
    string? Note);

public record CreateHomeSectionRequest(
    Guid CategoryId,
    string Theme,
    int DisplayOrder,
    int ProductsTake,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc);

public record UpdateHomeSectionRequest(
    Guid CategoryId,
    string Theme,
    int DisplayOrder,
    int ProductsTake,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive);

public record UpdateHomeContentSectionSettingRequest(
    bool IsEnabled);

public record CreateCouponRequest(
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
    IReadOnlyCollection<Guid>? VendorIds);

public record UpdateCouponRequest(
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
    IReadOnlyCollection<Guid>? VendorIds);

public record HomeSectionThemeOptionResponse(
    string Key,
    string LabelAr,
    string LabelEn);
