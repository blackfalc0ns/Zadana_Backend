using FluentAssertions;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Tests.Application.Geography;

public class GeoDistanceTests
{
    [Fact]
    public void Kilometers_DammamToKhobar_ShouldBeUnderThirty()
    {
        // Dammam ~26.3927,49.9777 ; Khobar ~26.2172,50.1971
        var km = GeoDistance.Kilometers(26.3927m, 49.9777m, 26.2172m, 50.1971m);
        km.Should().BeGreaterThan(20m);
        km.Should().BeLessThan(30m);
    }

    [Fact]
    public void HasUsableCoordinates_WhenEitherMissing_ShouldBeFalse()
    {
        GeoDistance.HasUsableCoordinates(null, 50m).Should().BeFalse();
        GeoDistance.HasUsableCoordinates(26m, null).Should().BeFalse();
        GeoDistance.HasUsableCoordinates(26.4m, 50.0m).Should().BeTrue();
    }
}
