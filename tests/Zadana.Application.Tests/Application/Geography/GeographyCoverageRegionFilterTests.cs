using FluentAssertions;
using Zadana.Application.Modules.Geography;

namespace Zadana.Application.Tests.Application.Geography;

public class GeographyCoverageRegionFilterTests
{
    [Theory]
    [InlineData(null, "all")]
    [InlineData("", "all")]
    [InlineData("all", "all")]
    [InlineData("ALL", "all")]
    [InlineData("All", "all")]
    [InlineData("eastern", "EASTERN")]
    [InlineData("EASTERN", "EASTERN")]
    public void Normalize_ShouldTreatAllRegionsCaseInsensitively(string? input, string expected)
    {
        GeographyRegionFilter.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void MatchesCity_WhenAllRegions_ShouldIncludeOfficialCities()
    {
        GeographyRegionFilter.MatchesCity(
                new AdminGeographyCoverageCityFilter("DAMMAM", "EASTERN"),
                "all")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void MatchesCity_WhenAllRegionsNormalizedToAllUppercase_ShouldStillIncludeCities()
    {
        GeographyRegionFilter.MatchesCity(
                new AdminGeographyCoverageCityFilter("DAMMAM", "EASTERN"),
                GeographyRegionFilter.Normalize("ALL"))
            .Should()
            .BeTrue();
    }
}
