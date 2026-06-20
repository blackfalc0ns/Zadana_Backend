using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryRegionMatcherTests
{
    [Theory]
    [InlineData("EASTERN", "Eastern Province", true)]
    [InlineData("EASTERN", "المنطقة الشرقية", true)]
    [InlineData("RIYADH", "Central Region", true)]
    [InlineData("EASTERN", "MAKKAH", false)]
    public void Matches_ShouldNormalizeRegionAliases(string left, string right, bool expected)
    {
        DeliveryRegionMatcher.Matches(left, right).Should().Be(expected);
    }
}
