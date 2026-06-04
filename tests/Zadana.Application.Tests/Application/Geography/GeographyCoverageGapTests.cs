using FluentAssertions;
using Zadana.Application.Modules.Geography;

namespace Zadana.Application.Tests.Application.Geography;

public class GeographyCoverageGapTests
{
    [Fact]
    public void BuildGapFlags_WhenCustomersWithoutVendorOrDriver_ShouldFlagAllRelevantGaps()
    {
        var gaps = GeographyCoverageGapRules.BuildGapFlags(
            customers: 12,
            activeVendors: 0,
            readyDrivers: 0);

        gaps.Should().Contain([
            GeographyCoverageConstants.GapFlags.NoVendor,
            GeographyCoverageConstants.GapFlags.NoDriver,
            GeographyCoverageConstants.GapFlags.NoSupply,
            GeographyCoverageConstants.GapFlags.DemandWithoutBoth]);
    }

    [Fact]
    public void BuildGapFlags_WhenNoCustomersAndNoSupply_ShouldReturnNoActivity()
    {
        GeographyCoverageGapRules.BuildGapFlags(customers: 0, activeVendors: 0, readyDrivers: 0)
            .Should()
            .ContainSingle(GeographyCoverageConstants.GapFlags.NoActivity);
    }

    [Fact]
    public void BuildGapFlags_WhenSupplyWithoutCustomers_ShouldReturnSupplyWithoutDemand()
    {
        GeographyCoverageGapRules.BuildGapFlags(customers: 0, activeVendors: 1, readyDrivers: 0)
            .Should()
            .ContainSingle(GeographyCoverageConstants.GapFlags.SupplyWithoutDemand);
    }

    [Fact]
    public void BuildGapFlags_WhenFullyCovered_ShouldReturnEmpty()
    {
        GeographyCoverageGapRules.BuildGapFlags(customers: 5, activeVendors: 2, readyDrivers: 1)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void HasOperationalGap_WhenOnlyNoActivity_ShouldBeFalse()
    {
        GeographyCoverageGapRules.HasOperationalGap([GeographyCoverageConstants.GapFlags.NoActivity])
            .Should()
            .BeFalse();
    }
}
