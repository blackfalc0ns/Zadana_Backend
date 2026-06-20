using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryPickupAreaMatcherTests
{
    [Fact]
    public void DriverMatchesDeliveryArea_WhenStoreAndCustomerCityMatchDriver_ShouldReturnTrue()
    {
        var driver = CreateDriver("KHOBAR");

        DeliveryPickupAreaMatcher.DriverMatchesDeliveryArea(driver, "الخبر", "Khobar")
            .Should().BeTrue();
    }

    [Fact]
    public void DriverMatchesDeliveryArea_WhenDriverCityDiffersFromStore_ShouldReturnFalse()
    {
        var driver = CreateDriver("DAMMAM");

        DeliveryPickupAreaMatcher.DriverMatchesDeliveryArea(driver, "Khobar", "Khobar")
            .Should().BeFalse();
    }

    [Fact]
    public void DriverMatchesDeliveryArea_WhenCustomerCityMissing_ShouldReturnFalse()
    {
        var driver = CreateDriver("KHOBAR");

        DeliveryPickupAreaMatcher.DriverMatchesDeliveryArea(driver, "Khobar", null)
            .Should().BeFalse();
    }

    [Fact]
    public void FilterDrivers_ShouldOnlyIncludeDriversInStoreAndCustomerCity()
    {
        var khobarDriver = CreateDriver("KHOBAR");
        var dammamDriver = CreateDriver("DAMMAM");

        var matches = DeliveryPickupAreaMatcher.FilterDrivers(
            [khobarDriver, dammamDriver],
            storeCity: "Khobar",
            customerCity: "الخبر");

        matches.Should().ContainSingle()
            .Which.Id.Should().Be(khobarDriver.Id);
    }

    private static Driver CreateDriver(string city)
    {
        var user = new User("driver@test.com", "+966500000001", "hash", UserRole.Driver);
        return new Driver(user.Id, DriverVehicleType.Car, "1234567890", "LIC-1", region: "EASTERN", city: city);
    }
}
