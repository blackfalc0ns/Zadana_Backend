using Zadana.Domain.Modules.Marketing.Enums;

namespace Zadana.Infrastructure.Caching;

public sealed record HomeCatalogProductSnapshot(
    Guid Id,
    Guid VendorProductId,
    DateTime CreatedAtUtc,
    Guid VendorId,
    Guid MasterProductId,
    Guid CategoryId,
    Guid? BrandId,
    bool BrandIsActive,
    string NameAr,
    string NameEn,
    string StoreAr,
    string StoreEn,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string? UnitAr,
    string? UnitEn,
    string ImageUrl,
    string? BrandNameAr,
    string? BrandNameEn,
    string? BrandLogo);

public sealed record HomeContentSectionSettingSnapshot(HomeContentSectionType SectionType, bool IsEnabled);

public sealed record HomeFeaturedPlacementSnapshot(
    FeaturedPlacementType PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId);

public sealed record HomeDynamicSectionSnapshot(
    Guid Id,
    Guid CategoryId,
    HomeSectionTheme Theme,
    int ProductsTake,
    string CategoryNameAr,
    string CategoryNameEn);
