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
        DeliveryEtaOperationalProfile profile,
        DeliveryEtaLiveSignal? liveSignal = null)
    {
        liveSignal ??= DeliveryEtaLiveSignal.None;

        var acceptanceMinutes = ResolveAcceptanceMinutes(profile);
        var prepMinutes = ResolvePreparationMinutes(preparationTimeMinutes, profile, out var prepFallback);
        var driverMinutes = ResolveDispatchMinutes(driverToVendorDistanceKm, profile, liveSignal);
        var customerMinutes = ResolveTravelMinutes(vendorToCustomerDistanceKm, VendorToCustomerMinutesPerKm, profile.AverageLastMileMinutes);
        var bufferMinutes = ResolveBuffer(profile, CheckoutOperationalBufferMinutes, liveSignal);
        var source = ResolveCheckoutSource(profile, liveSignal);
        var calculationMode = ResolveCalculationMode(profile, liveSignal);

        if (prepFallback && driverMinutes == 0 && customerMinutes == 0 && profile.SampleSize == 0)
        {
            return BuildWindow(
                profile.AverageTotalMinutes,
                minimumSpreadMinutes: 15,
                maximumSpreadMinutes: 25,
                confidence: "low",
                source: "distance_baseline",
                isApproximate: true,
                calculationMode: "distance_baseline",
                explanation: BuildExplanation("distance_baseline", profile, liveSignal, prepFallback),
                profile: profile);
        }

        var totalMinutes = HarmonizeOperationalTotal(
            acceptanceMinutes + prepMinutes + driverMinutes + customerMinutes + bufferMinutes,
            driverMinutes + customerMinutes,
            profile);
        var confidence = ResolveCheckoutConfidence(profile, prepFallback, driverMinutes, customerMinutes, liveSignal);

        return BuildWindow(
            totalMinutes,
            10,
            20,
            confidence,
            source,
            prepFallback || !profile.IsReliable,
            calculationMode,
            BuildExplanation(source, profile, liveSignal, prepFallback),
            profile);
    }

    public static DeliveryEtaEstimate? EstimateTracking(
        OrderStatus status,
        DateTime placedAtUtc,
        DateTime? deliveredAtUtc,
        DateTime? vendorAcceptedAtUtc,
        DateTime? driverAcceptedAtUtc,
        DateTime? pickedUpAtUtc,
        int? preparationTimeMinutes,
        decimal driverToVendorDistanceKm,
        decimal vendorToCustomerDistanceKm,
        DeliveryEtaOperationalProfile profile,
        DeliveryEtaLiveSignal? liveSignal = null,
        DeliveryEtaWindow? persistedWindow = null)
    {
        liveSignal ??= DeliveryEtaLiveSignal.None;

        if (status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return null;
        }

        if (status == OrderStatus.Delivered)
        {
            var actualDeliveredAt = deliveredAtUtc ?? placedAtUtc;
            return new DeliveryEtaEstimate(actualDeliveredAt, null);
        }

        var acceptanceMinutes = ResolveAcceptanceMinutes(profile);
        var prepMinutes = ResolvePreparationMinutes(preparationTimeMinutes, profile, out var prepFallback);
        var driverMinutes = ResolveDispatchMinutes(driverToVendorDistanceKm, profile, liveSignal);
        var customerMinutes = ResolveTravelMinutes(vendorToCustomerDistanceKm, VendorToCustomerMinutesPerKm, profile.AverageLastMileMinutes);
        var baseBuffer = ResolveBuffer(profile, TrackingOperationalBufferMinutes, liveSignal);
        var checkoutLikeTotal = HarmonizeOperationalTotal(
            acceptanceMinutes + prepMinutes + driverMinutes + customerMinutes + ResolveBuffer(profile, CheckoutOperationalBufferMinutes, liveSignal),
            driverMinutes + customerMinutes,
            profile);
        var seededTotal = persistedWindow is null
            ? checkoutLikeTotal
            : Math.Max(checkoutLikeTotal, (persistedWindow.MinMinutes + persistedWindow.MaxMinutes) / 2d);

        var (remainingMinutes, baseAtUtc, spreadMin, spreadMax, confidence, source, approximate) = status switch
        {
            OrderStatus.PendingVendorAcceptance =>
                ((int)Math.Round(seededTotal), placedAtUtc, 10, 20, ResolveHybridConfidence(profile, prepFallback), ResolveCheckoutSource(profile, liveSignal), prepFallback || !profile.IsReliable),
            OrderStatus.Accepted =>
                (
                    Math.Max(
                        10,
                        (int)Math.Round(
                            HarmonizeOperationalTotal(
                                prepMinutes + driverMinutes + customerMinutes + baseBuffer,
                                driverMinutes + customerMinutes,
                                profile,
                                capExtraMinutes: 18))),
                    vendorAcceptedAtUtc ?? placedAtUtc,
                    8,
                    15,
                    ResolveHybridConfidence(profile, prepFallback),
                    ResolveCheckoutSource(profile, liveSignal),
                    prepFallback || !profile.IsReliable),
            OrderStatus.Preparing =>
                (
                    Math.Max(
                        8,
                        (int)Math.Round(
                            HarmonizeOperationalTotal(
                                Math.Max(8, (int)Math.Round(prepMinutes * 0.7)) + driverMinutes + customerMinutes + baseBuffer,
                                driverMinutes + customerMinutes,
                                profile,
                                capExtraMinutes: 18))),
                    placedAtUtc,
                    10,
                    15,
                    ResolveHybridConfidence(profile, prepFallback),
                    ResolveCheckoutSource(profile, liveSignal),
                    prepFallback || !profile.IsReliable),
            OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress =>
                (
                    Math.Max(
                        8,
                        (int)Math.Round(
                            HarmonizeOperationalTotal(
                                driverMinutes + customerMinutes + baseBuffer,
                                driverMinutes + customerMinutes,
                                profile,
                                includePreparationBaseline: false,
                                capExtraMinutes: 15))),
                    DateTime.UtcNow,
                    8,
                    12,
                    liveSignal.HasHealthyCoverage || profile.IsReliable ? "high" : "medium",
                    "live_operational",
                    !profile.IsReliable && !liveSignal.HasHealthyCoverage),
            OrderStatus.DriverAssigned =>
                (
                    Math.Max(
                        5,
                        (int)Math.Round(
                            HarmonizeOperationalTotal(
                                Math.Max(5, (int)Math.Round(driverMinutes * 0.7)) + customerMinutes + Math.Max(4, baseBuffer - 2),
                                Math.Max(5, (int)Math.Round(driverMinutes * 0.7)) + customerMinutes,
                                profile,
                                includePreparationBaseline: false,
                                capExtraMinutes: 12))),
                    driverAcceptedAtUtc ?? DateTime.UtcNow,
                    6,
                    10,
                    "high",
                    "live_operational",
                    false),
            OrderStatus.PickedUp or OrderStatus.OnTheWay =>
                (
                    Math.Max(
                        5,
                        (int)Math.Round(
                            HarmonizeOperationalTotal(
                                Math.Max(5, customerMinutes) + 5,
                                Math.Max(5, customerMinutes),
                                profile,
                                includePreparationBaseline: false,
                                capExtraMinutes: 10))),
                    pickedUpAtUtc ?? driverAcceptedAtUtc ?? DateTime.UtcNow,
                    5,
                    8,
                    "high",
                    "live_operational",
                    false),
            _ =>
                ((int)Math.Round(profile.AverageTotalMinutes), placedAtUtc, 12, 20, "low", ResolveHistoricalSource(profile), true)
        };

        var estimatedAtUtc = baseAtUtc.AddMinutes(Math.Max(5, remainingMinutes));
        if (estimatedAtUtc < DateTime.UtcNow)
        {
            estimatedAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, Math.Min(20, spreadMax)));
        }

        return new DeliveryEtaEstimate(
            estimatedAtUtc,
            BuildWindow(
                remainingMinutes,
                spreadMin,
                spreadMax,
                confidence,
                source,
                approximate,
                status >= OrderStatus.ReadyForPickup ? "live_tracking_progressive" : ResolveCalculationMode(profile, liveSignal),
                BuildExplanation(source, profile, liveSignal, prepFallback),
                profile));
    }

    private static DeliveryEtaWindow BuildWindow(
        double totalMinutes,
        int minimumSpreadMinutes,
        int maximumSpreadMinutes,
        string confidence,
        string source,
        bool isApproximate,
        string? calculationMode,
        string? explanation,
        DeliveryEtaOperationalProfile? profile = null)
    {
        var roundedBase = RoundToFive(totalMinutes);
        var min = Math.Max(15, roundedBase - minimumSpreadMinutes);
        var max = Math.Max(min + 5, roundedBase + maximumSpreadMinutes);

        if (profile is not null && profile.SampleSize > 0)
        {
            var percentileMin = RoundToFive(Math.Max(15d, profile.Percentile50TotalMinutes - 5d));
            var percentileMax = RoundToFive(Math.Max(percentileMin + 5d, profile.Percentile80TotalMinutes + (profile.RecommendedBufferMinutes / 2d)));
            min = Math.Max(15, Math.Min(min, percentileMin));
            max = Math.Max(min + 5, Math.Max(max, percentileMax));
        }

        return new DeliveryEtaWindow(RoundToFive(min), RoundToFive(max), confidence, source, isApproximate, calculationMode, explanation);
    }

    private static int ResolveAcceptanceMinutes(DeliveryEtaOperationalProfile profile) =>
        profile.SampleSize > 0
            ? Math.Clamp((int)Math.Round(profile.AverageAcceptanceMinutes), 3, 15)
            : 5;

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

    private static int ResolveDispatchMinutes(
        decimal driverToVendorDistanceKm,
        DeliveryEtaOperationalProfile profile,
        DeliveryEtaLiveSignal liveSignal)
    {
        var dispatchMinutes = ResolveTravelMinutes(driverToVendorDistanceKm, DriverToVendorMinutesPerKm, profile.AverageDispatchLeadMinutes);
        if (liveSignal.NearbyAvailableDrivers <= 0)
        {
            return dispatchMinutes;
        }

        var nearestDriverMinutes = liveSignal.NearestAvailableDriverKm > 0
            ? DistanceToMinutes((decimal)liveSignal.NearestAvailableDriverKm, DriverToVendorMinutesPerKm)
            : 0;
        if (nearestDriverMinutes <= 0)
        {
            return dispatchMinutes;
        }

        var liveWeighted = liveSignal.HasHealthyCoverage
            ? (nearestDriverMinutes * 0.65d) + (dispatchMinutes * 0.35d)
            : (nearestDriverMinutes * 0.45d) + (dispatchMinutes * 0.55d);
        return Math.Max(4, (int)Math.Round(liveWeighted));
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

    private static int ResolveBuffer(DeliveryEtaOperationalProfile profile, int fallbackBuffer, DeliveryEtaLiveSignal? liveSignal = null)
    {
        var baseBuffer = profile.SampleSize > 0
            ? Math.Clamp(profile.RecommendedBufferMinutes, 6, 20)
            : fallbackBuffer;
        if (liveSignal is null)
        {
            return baseBuffer;
        }

        if (liveSignal.IsConstrained)
        {
            return Math.Min(24, baseBuffer + 4);
        }

        if (liveSignal.HasHealthyCoverage)
        {
            return Math.Max(6, baseBuffer - 2);
        }

        return baseBuffer;
    }

    private static double HarmonizeOperationalTotal(
        double computedMinutes,
        int travelMinutes,
        DeliveryEtaOperationalProfile profile,
        bool includePreparationBaseline = true,
        int capExtraMinutes = 20)
    {
        if (profile.SampleSize <= 0)
        {
            return computedMinutes;
        }

        var baselineMinutes = includePreparationBaseline
            ? profile.AverageTotalMinutes
            : Math.Max(10d, profile.AverageDispatchLeadMinutes + profile.AverageLastMileMinutes + profile.RecommendedBufferMinutes);

        var blendedMinutes = profile.IsReliable
            ? (computedMinutes * 0.35d) + (baselineMinutes * 0.65d)
            : (computedMinutes * 0.55d) + (baselineMinutes * 0.45d);

        var travelAwareCap = baselineMinutes + capExtraMinutes + Math.Min(10, Math.Max(0, travelMinutes - 20));
        var floor = Math.Max(includePreparationBaseline ? 15d : 8d, baselineMinutes * 0.75d);

        return Math.Clamp(blendedMinutes, floor, travelAwareCap);
    }

    private static string ResolveHybridConfidence(DeliveryEtaOperationalProfile profile, bool prepFallback) =>
        profile.IsReliable && !prepFallback
            ? "high"
            : profile.SampleSize > 0 || !prepFallback
                ? "medium"
                : "low";

    private static string ResolveCheckoutConfidence(
        DeliveryEtaOperationalProfile profile,
        bool prepFallback,
        int driverMinutes,
        int customerMinutes,
        DeliveryEtaLiveSignal liveSignal) =>
        profile.SampleSize <= 0
            ? "low"
            : profile.IsReliable && !prepFallback && profile.StageCoverageRatio >= 0.70m && (liveSignal.NearbyAvailableDrivers == 0 || liveSignal.HasHealthyCoverage)
                ? "high"
                : profile.SampleSize > 0 || driverMinutes > 0 || customerMinutes > 0 || !prepFallback
                    ? "medium"
                    : "low";

    private static string ResolveHistoricalSource(DeliveryEtaOperationalProfile profile) =>
        profile.SampleSize <= 0
            ? "distance_baseline"
            : profile.CalibrationSource switch
            {
                "branch_historical" => "branch_historical",
                "vendor_historical" => "vendor_historical",
                "regional_fallback" => "regional_fallback",
                _ => "distance_baseline"
            };

    private static string ResolveCheckoutSource(DeliveryEtaOperationalProfile profile, DeliveryEtaLiveSignal liveSignal) =>
        liveSignal.NearbyAvailableDrivers > 0 && profile.SampleSize > 0
            ? "live_operational"
            : ResolveHistoricalSource(profile);

    private static string ResolveCalculationMode(DeliveryEtaOperationalProfile profile, DeliveryEtaLiveSignal liveSignal) =>
        liveSignal.NearbyAvailableDrivers > 0 && profile.SampleSize > 0
            ? "historical_plus_live_dispatch"
            : profile.SampleSize > 0
                ? "historical_stage_blend"
                : "distance_baseline";

    private static string BuildExplanation(
        string source,
        DeliveryEtaOperationalProfile profile,
        DeliveryEtaLiveSignal liveSignal,
        bool prepFallback)
    {
        var livePart = liveSignal.NearbyAvailableDrivers > 0
            ? $"Nearby drivers: {liveSignal.NearbyAvailableDrivers}, nearest {Math.Round(liveSignal.NearestAvailableDriverKm, 1)} km."
            : "No fresh nearby driver signal.";
        var prepPart = prepFallback ? "Preparation fallback applied." : "Vendor preparation time available.";
        return $"{source} using {profile.SampleSize} historical orders. {prepPart} {livePart}";
    }
}

public static class DeliveryEtaWindowDisplayTextBuilder
{
    public static string BuildTitle() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? "وقت التوصيل المتوقع"
            : "Estimated delivery time";

    public static string BuildSubtitle(string confidence, bool isApproximate)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

        if (isApproximate || string.Equals(confidence, "low", StringComparison.OrdinalIgnoreCase))
        {
            return isArabic
                ? "الوقت تقديري وقد يتغير حسب حالة المتجر والمندوب."
                : "This time is approximate and may change based on store and driver status.";
        }

        return isArabic
            ? "راح تحديث الوقت حسب تقدم الطلب."
            : "This estimate will be updated as the order progresses.";
    }
}

public sealed record DeliveryEtaWindow(
    int MinMinutes,
    int MaxMinutes,
    string Confidence,
    string Source,
    bool IsApproximate,
    string? CalculationMode = null,
    string? Explanation = null);

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

