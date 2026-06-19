using System.Globalization;
using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Favorites.DTOs;

public record FavoriteItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("store")] string Store,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("rating")] decimal? Rating,
    [property: JsonPropertyName("review_count")] int? ReviewCount,
    [property: JsonPropertyName("discount")] string? Discount,
    [property: JsonPropertyName("is_favorite")] bool IsFavorite,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted,
    [property: JsonPropertyName("is_online_now")] bool IsOnlineNow = true,
    [property: JsonPropertyName("is_available_for_purchase")] bool IsAvailableForPurchase = true,
    [property: JsonPropertyName("unavailable_reason")] string? UnavailableReason = null,
    [property: JsonPropertyName("show_price_on_card")] bool ShowPriceOnCard = true);

public record FavoritesSummaryDto(
    [property: JsonPropertyName("items_count")] int ItemsCount);

public record FavoritesListResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<FavoriteItemDto> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("has_more")] bool HasMore,
    [property: JsonPropertyName("summary")] FavoritesSummaryDto Summary);

public record AddFavoriteResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("item")] FavoriteItemDto? Item,
    [property: JsonPropertyName("summary")] FavoritesSummaryDto Summary);

public record RemoveFavoriteResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("summary")] FavoritesSummaryDto Summary);

public record ClearFavoritesResponse(
    [property: JsonPropertyName("message")] string Message);

internal sealed record FavoriteOfferRow(
    Guid VendorProductId,
    Guid MasterProductId,
    Guid VendorId,
    DateTime CreatedAtUtc,
    string? NameAr,
    string? NameEn,
    string StoreAr,
    string StoreEn,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string? UnitAr,
    string? UnitEn,
    string? ImageUrl,
    bool ShowPriceOnCard);

internal sealed record FavoriteItemSource(
    Guid MasterProductId,
    DateTime CreatedAtUtc,
    string? NameAr,
    string? NameEn,
    string Name,
    string? StoreAr,
    string? StoreEn,
    string Store,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string? UnitAr,
    string? UnitEn,
    string? Unit,
    string? ImageUrl,
    decimal? Rating,
    int ReviewCount,
    bool IsOnlineNow = true,
    bool IsAvailableForPurchase = true,
    string? UnavailableReason = null,
    bool ShowPriceOnCard = true)
{
    public bool IsDiscounted => CompareAtPrice.HasValue && CompareAtPrice.Value > SellingPrice;
}

internal static class FavoriteProjectionMapper
{
    public static FavoriteItemDto Map(FavoriteItemSource item)
    {
        var discount = FormatDiscount(item.SellingPrice, item.CompareAtPrice);

        return new FavoriteItemDto(
            item.MasterProductId,
            item.Name,
            item.Store,
            item.SellingPrice,
            item.IsDiscounted ? item.CompareAtPrice : null,
            item.ImageUrl,
            item.Rating,
            item.ReviewCount,
            discount,
            true,
            item.Unit,
            item.IsDiscounted,
            item.IsOnlineNow,
            item.IsAvailableForPurchase,
            item.UnavailableReason,
            item.ShowPriceOnCard);
    }

    public static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    public static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? FormatDiscount(decimal price, decimal? oldPrice)
    {
        if (!oldPrice.HasValue || oldPrice.Value <= 0 || oldPrice.Value <= price)
        {
            return null;
        }

        var rate = (oldPrice.Value - price) / oldPrice.Value;
        return $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
