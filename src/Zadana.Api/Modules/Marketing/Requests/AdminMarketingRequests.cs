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
