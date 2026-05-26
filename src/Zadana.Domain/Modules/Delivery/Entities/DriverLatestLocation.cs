namespace Zadana.Domain.Modules.Delivery.Entities;

/// <summary>
/// Single-row-per-driver projection of the most recent driver location.
/// Maintained by the same handler that appends to <see cref="DriverLocation"/>.
/// Exists to make "where is driver X right now?" an O(1) primary-key lookup
/// instead of an indexed top-1-by-RecordedAtUtc scan, which becomes
/// expensive once the audit table grows past tens of millions of rows.
/// </summary>
public class DriverLatestLocation
{
    public Guid DriverId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal? AccuracyMeters { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Driver Driver { get; private set; } = null!;

    private DriverLatestLocation() { }

    public DriverLatestLocation(
        Guid driverId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters,
        DateTime recordedAtUtc)
    {
        DriverId = driverId;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        RecordedAtUtc = recordedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters,
        DateTime recordedAtUtc)
    {
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        RecordedAtUtc = recordedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
