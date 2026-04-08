using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Home.DTOs;

public record HomeHeaderDto(
    [property: JsonPropertyName("deliver_to_label")] string DeliverToLabel,
    [property: JsonPropertyName("location")] string Location,
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
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted);

public record HomeBrandCardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("cover_image")] string? CoverImage,
    [property: JsonPropertyName("product_count")] int ProductCount,
    [property: JsonPropertyName("description")] string? Description);

public record HomeContentDto(
    [property: JsonPropertyName("deliver_to_label")] string DeliverToLabel,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("notifications_count")] int NotificationsCount,
    [property: JsonPropertyName("banners")] IReadOnlyList<HomeBannerDto> Banners,
    [property: JsonPropertyName("categories")] IReadOnlyList<HomeCategoryDto> Categories,
    [property: JsonPropertyName("special_offers")] IReadOnlyList<HomeProductCardDto> SpecialOffers,
    [property: JsonPropertyName("recommended")] IReadOnlyList<HomeProductCardDto> Recommended,
    [property: JsonPropertyName("best_selling")] IReadOnlyList<HomeProductCardDto> BestSelling,
    [property: JsonPropertyName("brands")] IReadOnlyList<HomeBrandCardDto> Brands,
    [property: JsonPropertyName("featured_products")] IReadOnlyList<HomeProductCardDto> FeaturedProducts,
    [property: JsonPropertyName("explore_more")] IReadOnlyList<HomeProductCardDto> ExploreMore);
