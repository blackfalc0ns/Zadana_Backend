using System.Globalization;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class DeliveryEtaPolicy
{
    private const double DriverToVendorMinutesPerKm = 4.5;
    private const double VendorToCustomerMinutesPerKm = 3.8;
    private const int CheckoutOperationalBufferMinutes = 12;
    private const int TrackingOperationalBufferMinutes = 8;

    public static DeliveryEtaWindow EstimateCheckoutWindow(
        int? preparationTimeMinutes,
        decimal driverToVendorDistanceKm,
        decimal vendorToCustomerDistanceKm,
        DeliveryEtaOperationalProfile profile)
    {
        var prepMinutes = ResolvePreparationMinutes(preparationTimeMinutes, profile, out var prepFallback);
        var driverMinutes = ResolveTravelMinutes(driverToVendorDistanceKm, DriverToVendorMinutesPerKm, profile.AverageDispatchLeadMinutes);
        var customerMinutes = ResolveTravelMinutes(vendorToCustomerDistanceKm, VendorToCustomerMinutesPerKm, profile.AverageLastMileMinutes);
        var bufferMinutes = ResolveBuffer(profile, CheckoutOperationalBufferMinutes);

        if (prepFallback && driverMinutes == 0 && customerMinutes == 0 && profile.SampleSize == 0)
        {
            return BuildWindow(
                profile.AverageTotalMinutes,
                minimumSpreadMinutes: 15,
                maximumSpreadMinutes: 25,
                confidence: "low",
                source: "historical_fallback",
                isApproximate: true);
        }

        var totalMinutes = prepMinutes + driverMinutes + customerMinutes + bufferMinutes;
        var confidence = profile.IsReliable && !prepFallback
            ? "high"
            : profile.SampleSize > 0 || !prepFallback
                ? "medium"
                : "low";
        var source = profile.SampleSize > 0 || driverMinutes > 0 || customerMinutes > 0
            ? "hybrid_operational"
            : "historical_fallback";
        return BuildWindow(totalMinutes, 10, 20, confidence, source, prepFallback || !profile.IsReliable);
    }

    public static DeliveryEtaEstimate? EstimateTracking(
        OrderStatus status,
        DateTime placedAtUtc,
        DateTime? deliveredAtUtc,
        DateTime? driverAcceptedAtUtc,
        DateTime? pickedUpAtUtc,
        int? preparationTimeMinutes,
        decimal driverToVendorDistanceKm,
        decimal vendorToCustomerDistanceKm,
        DeliveryEtaOperationalProfile profile)
    {
        if (status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return null;
        }

        if (status == OrderStatus.Delivered)
        {
            var actualDeliveredAt = deliveredAtUtc ?? placedAtUtc;
            return new DeliveryEtaEstimate(actualDeliveredAt, null);
        }

        var prepMinutes = ResolvePreparationMinutes(preparationTimeMinutes, profile, out var prepFallback);
        var driverMinutes = ResolveTravelMinutes(driverToVendorDistanceKm, DriverToVendorMinutesPerKm, profile.AverageDispatchLeadMinutes);
        var customerMinutes = ResolveTravelMinutes(vendorToCustomerDistanceKm, VendorToCustomerMinutesPerKm, profile.AverageLastMileMinutes);
        var baseBuffer = ResolveBuffer(profile, TrackingOperationalBufferMinutes);

        var (remainingMinutes, baseAtUtc, spreadMin, spreadMax, confidence, source, approximate) = status switch
        {
            OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted =>
                (prepMinutes + driverMinutes + customerMinutes + ResolveBuffer(profile, CheckoutOperationalBufferMinutes), placedAtUtc, 10, 20, ResolveHybridConfidence(profile, prepFallback), ResolveHybridSource(profile), prepFallback || !profile.IsReliable),
            OrderStatus.Preparing =>
                (Math.Max(8, (int)Math.Round(prepMinutes * 0.7)) + driverMinutes + customerMinutes + baseBuffer, placedAtUtc, 10, 15, ResolveHybridConfidence(profile, prepFallback), ResolveHybridSource(profile), prepFallback || !profile.IsReliable),
            OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress =>
                (driverMinutes + customerMinutes + baseBuffer, DateTime.UtcNow, 8, 12, profile.IsReliable ? "high" : "medium", "hybrid_operational", !profile.IsReliable),
            OrderStatus.DriverAssigned =>
                (Math.Max(5, (int)Math.Round(driverMinutes * 0.7)) + customerMinutes + Math.Max(4, baseBuffer - 2), driverAcceptedAtUtc ?? DateTime.UtcNow, 6, 10, "high", "live_tracking_refined", false),
            OrderStatus.PickedUp or OrderStatus.OnTheWay =>
                (Math.Max(5, customerMinutes) + 5, pickedUpAtUtc ?? driverAcceptedAtUtc ?? DateTime.UtcNow, 5, 8, "high", "live_tracking_refined", false),
            _ =>
                ((int)Math.Round(profile.AverageTotalMinutes), placedAtUtc, 12, 20, "low", "historical_fallback", true)
        };

        var estimatedAtUtc = baseAtUtc.AddMinutes(Math.Max(5, remainingMinutes));
        if (estimatedAtUtc < DateTime.UtcNow)
        {
            estimatedAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, Math.Min(20, spreadMax)));
        }

        return new DeliveryEtaEstimate(
            estimatedAtUtc,
            BuildWindow(remainingMinutes, spreadMin, spreadMax, confidence, source, approximate));
    }

    private static DeliveryEtaWindow BuildWindow(
        double totalMinutes,
        int minimumSpreadMinutes,
        int maximumSpreadMinutes,
        string confidence,
        string source,
        bool isApproximate)
    {
        var roundedBase = RoundToFive(totalMinutes);
        var min = Math.Max(15, roundedBase - minimumSpreadMinutes);
        var max = Math.Max(min + 5, roundedBase + maximumSpreadMinutes);
        return new DeliveryEtaWindow(RoundToFive(min), RoundToFive(max), confidence, source, isApproximate);
    }

    private static int ResolvePreparationMinutes(int? preparationTimeMinutes, DeliveryEtaOperationalProfile profile, out bool usedFallback)
    {
        if (preparationTimeMinutes.HasValue && preparationTimeMinutes.Value > 0)
        {
            usedFallback = false;
            return preparationTimeMinutes.Value;
        }

        usedFallback = true;
        var derivedPrep = profile.SampleSize > 0
            ? profile.AveragePreparationMinutes
            : profile.AverageTotalMinutes * 0.45;
        return Math.Clamp((int)Math.Round(derivedPrep), 12, 35);
    }

    private static int ResolveTravelMinutes(decimal distanceKm, double minutesPerKm, double observedStageMinutes)
    {
        var distanceMinutes = DistanceToMinutes(distanceKm, minutesPerKm);
        if (distanceMinutes > 0 && observedStageMinutes > 0)
        {
            return Math.Max(4, (int)Math.Round((distanceMinutes * 0.65) + (observedStageMinutes * 0.35)));
        }

        if (distanceMinutes > 0)
        {
            return distanceMinutes;
        }

        return observedStageMinutes > 0
            ? Math.Max(4, (int)Math.Round(observedStageMinutes))
            : 0;
    }

    private static int DistanceToMinutes(decimal distanceKm, double minutesPerKm)
    {
        if (distanceKm <= 0m)
        {
            return 0;
        }

        return Math.Max(4, (int)Math.Round((double)distanceKm * minutesPerKm));
    }

    private static int RoundToFive(double value) =>
        Math.Max(5, (int)(Math.Round(value / 5d, MidpointRounding.AwayFromZero) * 5));

    private static int ResolveBuffer(DeliveryEtaOperationalProfile profile, int fallbackBuffer) =>
        profile.SampleSize > 0
            ? Math.Clamp(profile.RecommendedBufferMinutes, 6, 20)
            : fallbackBuffer;

    private static string ResolveHybridConfidence(DeliveryEtaOperationalProfile profile, bool prepFallback) =>
        profile.IsReliable && !prepFallback
            ? "high"
            : profile.SampleSize > 0 || !prepFallback
                ? "medium"
                : "low";

    private static string ResolveHybridSource(DeliveryEtaOperationalProfile profile) =>
        profile.SampleSize > 0 ? "hybrid_operational" : "historical_fallback";
}

public sealed record DeliveryEtaWindow(
    int MinMinutes,
    int MaxMinutes,
    string Confidence,
    string Source,
    bool IsApproximate);

public sealed record DeliveryEtaEstimate(
    DateTime DatetimeUtc,
    DeliveryEtaWindow? Window);

public static class DeliveryEtaWindowLabelBuilder
{
    public static string Build(int minMinutes, int maxMinutes, bool isApproximate)
    {
        var windowText = $"{minMinutes}-{maxMinutes} {(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase) ? "دقيقة" : "minutes")}";
        return isApproximate
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
                ? $"حوالي {windowText}"
                : $"Around {windowText}"
            : windowText;
    }
}
