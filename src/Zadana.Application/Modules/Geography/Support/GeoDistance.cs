namespace Zadana.Application.Modules.Geography.Support;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371d;

    public static bool HasUsableCoordinates(decimal? latitude, decimal? longitude) =>
        latitude.HasValue && longitude.HasValue;

    public static decimal Kilometers(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;
        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        return (decimal)(Math.Sqrt(x * x + y * y) * EarthRadiusKm);
    }

    public static bool TryKilometers(
        decimal? lat1,
        decimal? lng1,
        decimal? lat2,
        decimal? lng2,
        out decimal kilometers)
    {
        kilometers = 0m;
        if (!HasUsableCoordinates(lat1, lng1) || !HasUsableCoordinates(lat2, lng2))
        {
            return false;
        }

        kilometers = Kilometers(lat1!.Value, lng1!.Value, lat2!.Value, lng2!.Value);
        return true;
    }
}
