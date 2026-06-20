using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryPickupAreaMatcher
{
    /// <summary>
    /// Driver must operate in the store (branch) city and the customer delivery city.
    /// When both cities are set they must normalize to the same city for a match.
    /// </summary>
    public static bool DriverMatchesDeliveryArea(Driver driver, string? storeCity, string? customerCity)
    {
        if (!DeliveryCityMatcher.Matches(driver.City, storeCity))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(customerCity))
        {
            return false;
        }

        return DeliveryCityMatcher.Matches(driver.City, customerCity);
    }

    public static List<Driver> FilterDrivers(
        IEnumerable<Driver> drivers,
        string? storeCity,
        string? customerCity) =>
        drivers
            .Where(driver => DriverMatchesDeliveryArea(driver, storeCity, customerCity))
            .ToList();
}
