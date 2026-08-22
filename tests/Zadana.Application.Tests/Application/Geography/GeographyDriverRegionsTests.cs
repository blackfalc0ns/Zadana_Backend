using FluentAssertions;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Tests.Application.Geography;

public class GeographyDriverRegionsTests
{
    [Fact]
    public void DriverShowsCityPicker_ShouldBeFalse()
    {
        OperationalGeographyScope.DriverShowsCityPicker.Should().BeFalse();
    }

    [Fact]
    public void EasternRegionCode_ShouldBeOnlyOperationalRegion()
    {
        OperationalGeographyScope.EasternRegionCode.Should().Be("EASTERN");
    }
}
