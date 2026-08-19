using Zadana.Application.Modules.Geography.Support;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryPickupAreaMatcher
{
    public static bool DriverMatchesPickup(
        decimal? driverLatitude,
        decimal? driverLongitude,
        decimal? pickupLatitude,
        decimal? pickupLongitude,
        bool gpsFresh)
    {
        if (!gpsFresh)
        {
            return false;
        }

        if (!GeoDistance.TryKilometers(
                driverLatitude,
                driverLongitude,
                pickupLatitude,
                pickupLongitude,
                out var km))
        {
            return false;
        }

        return km <= DeliveryProximityLimits.MaxMatchKm;
    }

    public static bool DriverMatchesDeliveryArea(Driver driver, string? storeCity, string? customerCity)
    {
        _ = driver;
        _ = storeCity;
        _ = customerCity;
        return true;
    }

    public static List<Driver> FilterDrivers(
        IEnumerable<Driver> drivers,
        string? storeCity,
        string? customerCity)
    {
        _ = storeCity;
        _ = customerCity;
        return drivers.ToList();
    }
}
