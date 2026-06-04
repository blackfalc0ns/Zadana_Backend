using FluentAssertions;
using Zadana.Infrastructure.Data;

namespace Zadana.Application.Tests.Application.Geography;

public class SaudiGeographyCatalogTests
{
    [Fact]
    public void Catalog_ShouldIncludeAllThirteenRegions()
    {
        SaudiGeographyCatalog.Regions.Should().HaveCount(13);
        SaudiGeographyCatalog.Regions.Select(region => region.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_ShouldIncludeGovernorateLevelCitiesForEveryRegion()
    {
        SaudiGeographyCatalog.Cities.Should().HaveCountGreaterThanOrEqualTo(100);
        SaudiGeographyCatalog.Cities.Select(city => city.Code).Should().OnlyHaveUniqueItems();

        foreach (var region in SaudiGeographyCatalog.Regions)
        {
            SaudiGeographyCatalog.Cities
                .Count(city => city.RegionCode == region.Code)
                .Should()
                .BeGreaterThan(0, $"region {region.Code} should have at least one city");
        }
    }
}
