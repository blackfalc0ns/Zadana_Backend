using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryPickupAreaMatcher
{
    public static bool DriverMatchesPickup(Driver driver, string? pickupCity, string? pickupRegion)
    {
        if (DeliveryCityMatcher.Matches(driver.City, pickupCity))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(pickupRegion)
            && DeliveryRegionMatcher.Matches(driver.Region, pickupRegion);
    }

    public static List<Driver> FilterDrivers(
        IEnumerable<Driver> drivers,
        string? pickupCity,
        string? pickupRegion)
    {
        var driverList = drivers.ToList();

        var cityMatches = driverList
            .Where(driver => DeliveryCityMatcher.Matches(driver.City, pickupCity))
            .ToList();

        if (cityMatches.Count > 0)
        {
            return cityMatches;
        }

        if (string.IsNullOrWhiteSpace(pickupRegion))
        {
            return cityMatches;
        }

        return driverList
            .Where(driver => DeliveryRegionMatcher.Matches(driver.Region, pickupRegion))
            .ToList();
    }
}
