namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryProximityLimits
{
    public const decimal MaxMatchKm = 50m;
    public static readonly TimeSpan GpsFreshnessThreshold = TimeSpan.FromMinutes(5);
}
