namespace Zadana.Domain.Modules.Delivery.Enums;

public enum DriverVehicleType
{
    Car,
    Motorcycle,
    Scooter,
    Van,
    Bicycle,
    Truck,

    // Backward-compatible alias for older seeded or imported driver records.
    Motorbike = Motorcycle
}
