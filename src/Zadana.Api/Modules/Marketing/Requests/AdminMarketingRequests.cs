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

public record UpdateFeaturedProductSelectionSettingsRequest(
    string SelectionMode,
    int TargetCount,
    int MinSalesCount,
    int MinStoreCount,
    bool RequireDiscount,
    bool ExcludeProductsAlreadyInSpecialOffers);

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

public record UpsertPlatformContactSettingsRequest(
    string? SupportEmail,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TwitterUrl,
    string? TikTokUrl,
    string? SnapchatUrl,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? LinkedInUrl);

public record PlatformContactSettingsDto(
    string? SupportEmail,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TwitterUrl,
    string? TikTokUrl,
    string? SnapchatUrl,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? LinkedInUrl,
    DateTime? UpdatedAtUtc);

public record UpsertPlatformLegalDocumentRequest(
    string? ContentAr,
    string? ContentEn,
    string? Version,
    DateTime? EffectiveAtUtc);

public record PlatformLegalDocumentDto(
    string DocumentType,
    string ContentAr,
    string ContentEn,
    string Version,
    DateTime EffectiveAtUtc,
    DateTime UpdatedAtUtc);
