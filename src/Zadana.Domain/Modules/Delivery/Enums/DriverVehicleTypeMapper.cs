namespace Zadana.Domain.Modules.Delivery.Enums;

public static class DriverVehicleTypeMapper
{
    public static bool TryParse(string? value, out DriverVehicleType vehicleType)
    {
        vehicleType = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);

        return normalized.ToLowerInvariant() switch
        {
            "car" => Parse(DriverVehicleType.Car, out vehicleType),
            "motorcycle" => Parse(DriverVehicleType.Motorcycle, out vehicleType),
            "motorbike" => Parse(DriverVehicleType.Motorcycle, out vehicleType),
            "scooter" => Parse(DriverVehicleType.Scooter, out vehicleType),
            "van" => Parse(DriverVehicleType.Van, out vehicleType),
            "cargo van" => Parse(DriverVehicleType.Van, out vehicleType),
            "bicycle" => Parse(DriverVehicleType.Bicycle, out vehicleType),
            "bike" => Parse(DriverVehicleType.Bicycle, out vehicleType),
            "truck" => Parse(DriverVehicleType.Truck, out vehicleType),
            _ => Enum.TryParse(value.Trim(), ignoreCase: true, out vehicleType)
        };
    }

    public static DriverVehicleType? ParseOrNull(string? value) =>
        TryParse(value, out var vehicleType) ? vehicleType : null;

    public static string? ToStorageValue(DriverVehicleType? vehicleType) =>
        vehicleType switch
        {
            null => null,
            DriverVehicleType.Motorbike => nameof(DriverVehicleType.Motorcycle),
            _ => vehicleType.Value.ToString()
        };

    private static bool Parse(DriverVehicleType parsedValue, out DriverVehicleType vehicleType)
    {
        vehicleType = parsedValue;
        return true;
    }
}
