using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryPickupAreaMatcherTests
{
    [Fact]
    public void DriverMatchesPickup_WhenFreshGpsInsideFiftyKm_ShouldBeTrue()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            driverLatitude: 26.22m,
            driverLongitude: 50.19m,
            pickupLatitude: 26.39m,
            pickupLongitude: 49.98m,
            gpsFresh: true).Should().BeTrue();
    }

    [Fact]
    public void DriverMatchesPickup_WhenFreshGpsBeyondFiftyKm_ShouldBeFalse()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            driverLatitude: 24.71m,
            driverLongitude: 46.68m,
            pickupLatitude: 26.39m,
            pickupLongitude: 49.98m,
            gpsFresh: true).Should().BeFalse();
    }

    [Fact]
    public void DriverMatchesPickup_WhenWithinCustomRing_ShouldRespectMaxRadius()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            26.26m,
            50.19m,
            26.22m,
            50.19m,
            gpsFresh: true,
            maxRadiusKm: 5m).Should().BeTrue();

        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            26.30m,
            50.19m,
            26.22m,
            50.19m,
            gpsFresh: true,
            maxRadiusKm: 5m).Should().BeFalse();

        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            26.30m,
            50.19m,
            26.22m,
            50.19m,
            gpsFresh: true,
            maxRadiusKm: 10m).Should().BeTrue();
    }

    [Fact]
    public void DriverMatchesPickup_WhenGpsStale_ShouldBeFalse()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            26.22m, 50.19m, 26.39m, 49.98m, gpsFresh: false).Should().BeFalse();
    }
}
