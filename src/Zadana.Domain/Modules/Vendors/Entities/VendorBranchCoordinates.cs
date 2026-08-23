namespace Zadana.Domain.Modules.Vendors.Entities;

public static class VendorBranchCoordinates
{
    public static bool IsUnset(decimal latitude, decimal longitude) =>
        latitude == 0m && longitude == 0m;

    public static bool IsInRange(decimal latitude, decimal longitude) =>
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    public static bool IsValid(decimal latitude, decimal longitude) =>
        IsInRange(latitude, longitude) && !IsUnset(latitude, longitude);

    public static void EnsureValid(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
            throw new InvalidOperationException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180)
            throw new InvalidOperationException("Longitude must be between -180 and 180.");
        if (IsUnset(latitude, longitude))
            throw new InvalidOperationException("Branch coordinates must be a real map location.");
    }
}
