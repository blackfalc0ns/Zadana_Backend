namespace Zadana.Domain.Modules.Delivery.Entities;

public class DriverLocation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DriverId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    // Navigation
    public Driver Driver { get; private set; } = null!;

    private DriverLocation() { }

    public DriverLocation(Guid driverId, decimal latitude, decimal longitude)
    {
        DriverId = driverId;
        Latitude = latitude;
        Longitude = longitude;
        RecordedAtUtc = DateTime.UtcNow;
    }
}
