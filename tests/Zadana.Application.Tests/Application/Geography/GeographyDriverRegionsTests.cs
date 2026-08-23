using FluentAssertions;
using Zadana.Application.Modules.Geography;
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

    [Fact]
    public void LocalizeRegion_Eastern_ShouldIncludeMetroCities()
    {
        SaudiGeographyDisplay.LocalizeRegion("EASTERN", arabic: true)
            .Should().Be("المنطقة الشرقية (الدمام - الظهران - الخبر)");
        SaudiGeographyDisplay.LocalizeRegion("EASTERN", arabic: false)
            .Should().Be("Eastern Region (Dammam, Dhahran, Khobar)");
    }
}
