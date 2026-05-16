using System.Globalization;
using FluentAssertions;
using Zadana.Application.Common.Caching;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries;

namespace Zadana.Application.Tests.Application.Caching;

public class CatalogCachingTests
{
    [Fact]
    public void SearchProductsCacheKey_ShouldIncludeCultureAndNormalizedFilters()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("ar");

        try
        {
            var key = CatalogQueryCacheKeys.SearchProducts(
                "  AC Unit  ",
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                12.5m,
                99.99m,
                "price_low_high",
                2,
                24);

            key.Should().Contain(":ar:");
            key.Should().Contain("11111111111111111111111111111111");
            key.Should().Contain("22222222222222222222222222222222");
            key.Should().Contain(":12.5:");
            key.Should().Contain(":99.99:");
            key.Should().Contain(":price-low-high:");
            key.Should().EndWith(":2:24:public");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void ProductDetailsFavoriteOverlay_ShouldOnlyFlipFavoriteFlags()
    {
        var response = new ProductDetailsDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Main Product",
            "Store",
            10m,
            15m,
            "image.png",
            ["image.png"],
            4.6m,
            12,
            "33%",
            false,
            "kg",
            true,
            "desc",
            [],
            [],
            [
                new ProductDetailsSimilarProductDto(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    "Similar 1",
                    "Store",
                    20m,
                    null,
                    "similar-1.png",
                    4.2m,
                    8,
                    null,
                    false,
                    "kg",
                    false),
                new ProductDetailsSimilarProductDto(
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    "Similar 2",
                    "Store",
                    30m,
                    null,
                    "similar-2.png",
                    4.1m,
                    5,
                    null,
                    false,
                    "kg",
                    false)
            ]);

        var favorites = new HashSet<Guid>
        {
            response.MasterProductId,
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")
        };

        var updated = CatalogQueryFavoriteOverlays.ApplyFavorites(response, favorites);

        updated.IsFavorite.Should().BeTrue();
        updated.Name.Should().Be(response.Name);
        updated.SimilarProducts[0].IsFavorite.Should().BeFalse();
        updated.SimilarProducts[1].IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void CatalogInvalidationProfile_ShouldCoverCatalogFiltersAndHome()
    {
        CacheInvalidationProfiles.CatalogReadModels.Should().BeEquivalentTo(
            [CacheTagNames.Catalog, CacheTagNames.CatalogFilters, CacheTagNames.Home]);
    }
}
