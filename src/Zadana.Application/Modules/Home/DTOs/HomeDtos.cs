using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Home.DTOs;

public record HomeHeaderDto(
    [property: JsonPropertyName("deliver_to_label")] string DeliverToLabel,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("address_line")] string AddressLine,
    [property: JsonPropertyName("notifications_count")] int NotificationsCount);

public record HomeBannerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("action_label")] string? ActionLabel,
    [property: JsonPropertyName("image_url")] string ImageUrl);

public record HomeCategoryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl);

public record HomeProductCardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("store")] string Store,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("image_url")] string ImageUrl,
    [property: JsonPropertyName("rating")] decimal? Rating,
    [property: JsonPropertyName("review_count")] int? ReviewCount,
    [property: JsonPropertyName("discount")] string? Discount,
    [property: JsonPropertyName("is_favorite")] bool IsFavorite,
    [property: JsonPropertyName("is_featured")] bool IsFeatured,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted);

public record HomeBrandCardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("cover_image")] string? CoverImage,
    [property: JsonPropertyName("product_count")] int ProductCount,
    [property: JsonPropertyName("description")] string? Description);

public record HomeListSectionDto<TItem>(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("items")] IReadOnlyList<TItem> Items);

public record HomeDynamicSectionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subcategory_id")] Guid SubcategoryId,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("theme")] string Theme,
    [property: JsonPropertyName("theme_label")] string ThemeLabel,
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("items")] IReadOnlyList<HomeProductCardDto> Items);

public record HomeContentDto(
    [property: JsonPropertyName("deliver_to_label")] string DeliverToLabel,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("address_line")] string AddressLine,
    [property: JsonPropertyName("notifications_count")] int NotificationsCount,
    [property: JsonPropertyName("banners_section")] HomeListSectionDto<HomeBannerDto> BannersSection,
    [property: JsonPropertyName("categories_section")] HomeListSectionDto<HomeCategoryDto> CategoriesSection,
    [property: JsonPropertyName("special_offers_section")] HomeListSectionDto<HomeProductCardDto> SpecialOffersSection,
    [property: JsonPropertyName("recommended_section")] HomeListSectionDto<HomeProductCardDto> RecommendedSection,
    [property: JsonPropertyName("best_selling_section")] HomeListSectionDto<HomeProductCardDto> BestSellingSection,
    [property: JsonPropertyName("brands_section")] HomeListSectionDto<HomeBrandCardDto> BrandsSection,
    [property: JsonPropertyName("featured_products_section")] HomeListSectionDto<HomeProductCardDto> FeaturedProductsSection,
    [property: JsonPropertyName("explore_more_section")] HomeListSectionDto<HomeProductCardDto> ExploreMoreSection,
    [property: JsonPropertyName("dynamic_sections")] IReadOnlyList<HomeDynamicSectionDto> DynamicSections);
