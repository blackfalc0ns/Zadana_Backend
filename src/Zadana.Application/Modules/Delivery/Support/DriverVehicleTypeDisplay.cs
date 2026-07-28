using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Modules.Delivery.Support;

/// <summary>
/// Localized display labels for <see cref="DriverVehicleType"/> in bilingual notifications/APIs.
/// </summary>
public static class DriverVehicleTypeDisplay
{
    public static string Localize(DriverVehicleType? vehicleType, bool arabic) =>
        vehicleType switch
        {
            DriverVehicleType.Car => arabic ? "سيارة" : "Car",
            DriverVehicleType.Motorcycle => arabic ? "دراجة نارية" : "Motorcycle",
            DriverVehicleType.Scooter => arabic ? "سكوتر" : "Scooter",
            DriverVehicleType.Van => arabic ? "فان" : "Van",
            DriverVehicleType.Bicycle => arabic ? "دراجة هوائية" : "Bicycle",
            DriverVehicleType.Truck => arabic ? "شاحنة" : "Truck",
            _ => arabic ? "مركبة توصيل" : "Delivery vehicle"
        };

    public static (string Ar, string En) LocalizePair(DriverVehicleType? vehicleType) =>
        (Localize(vehicleType, arabic: true), Localize(vehicleType, arabic: false));
}
