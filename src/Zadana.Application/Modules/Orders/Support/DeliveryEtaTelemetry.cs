using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class DeliveryEtaTelemetry
{
    private const int MinimumReliableSampleSize = 5;
    private const int MinimumBucketSampleSize = 3;
    private const int RecentHistoricalSampleLimit = 60;
    private const double DefaultAverageTotalMinutes = 45d;
    private const int LiveDriverFreshnessMinutes = 20;

    public static async Task<DeliveryEtaOperationalProfile> LoadOperationalProfileAsync(
        IApplicationDbContext context,
        Guid vendorId,
        Guid? vendorBranchId,
        string? customerCity,
        string? customerArea,
        CancellationToken cancellationToken)
    {
        var referenceUtc = DateTime.UtcNow;

        if (vendorBranchId.HasValue)
        {
            var branchSnapshots = await LoadSnapshotsAsync(context, vendorId, vendorBranchId, cancellationToken);
            var branchProfile = ResolveBestProfile(branchSnapshots, "branch_historical", referenceUtc);
            if (branchProfile.IsReliable || branchProfile.SampleSize >= MinimumBucketSampleSize)
            {
                return branchProfile;
            }
        }

        var vendorSnapshots = await LoadSnapshotsAsync(context, vendorId, null, cancellationToken);
        var vendorProfile = ResolveBestProfile(vendorSnapshots, "vendor_historical", referenceUtc);
        return vendorProfile.IsReliable || vendorProfile.SampleSize > 0
            ? vendorProfile
            : await LoadRegionalFallbackProfileAsync(context, customerCity, customerArea, referenceUtc, cancellationToken);
    }

    public static async Task<DeliveryEtaLiveSignal> LoadLiveSignalAsync(
        IApplicationDbContext context,
        Guid? vendorBranchId,
        CancellationToken cancellationToken)
    {
        if (!vendorBranchId.HasValue)
        {
            return DeliveryEtaLiveSignal.None;
        }

        var branch = await context.VendorBranches
            .AsNoTracking()
            .Where(item => item.Id == vendorBranchId.Value)
            .Select(item => new
            {
                item.Latitude,
                item.Longitude
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (branch is null)
        {
            return DeliveryEtaLiveSignal.None;
        }

        var freshnessCutoff = DateTime.UtcNow.AddMinutes(-LiveDriverFreshnessMinutes);
        var drivers = await context.Drivers
            .AsNoTracking()
            .Where(driver =>
                driver.VerificationStatus == Domain.Modules.Delivery.Enums.DriverVerificationStatus.Approved &&
                driver.Status == Domain.Modules.Identity.Enums.AccountStatus.Active &&
                driver.IsAvailable &&
                !driver.IsLocationUpdatesBlocked)
            .Select(driver => new
            {
                driver.Id,
                driver.City,
                LatestLocation = driver.Locations
                    .OrderByDescending(location => location.RecordedAtUtc)
                    .Select(location => new
                    {
                        location.Latitude,
                        location.Longitude,
                        location.RecordedAtUtc
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var nearbyDistances = drivers
            .Where(driver =>
                driver.LatestLocation is not null &&
                driver.LatestLocation.RecordedAtUtc >= freshnessCutoff)
            .Select(driver => CalculateDistanceKm(
                branch.Latitude,
                branch.Longitude,
                driver.LatestLocation!.Latitude,
                driver.LatestLocation.Longitude))
            .OrderBy(distance => distance)
            .ToArray();

        if (nearbyDistances.Length == 0)
        {
            return DeliveryEtaLiveSignal.None;
        }

        var nearestDriverKm = nearbyDistances[0];
        var nearbyAvailableDrivers = nearbyDistances.Count(distance => distance <= 8d);
        return new DeliveryEtaLiveSignal(
            nearbyAvailableDrivers,
            nearestDriverKm,
            nearbyAvailableDrivers >= 3,
            nearbyAvailableDrivers == 0 || nearestDriverKm > 10d);
    }

    public static DeliveryEtaOperationalProfile BuildOperationalProfile(
        IReadOnlyCollection<DeliveryEtaHistoricalSnapshot> snapshots,
        string calibrationSource)
    {
        if (snapshots.Count == 0)
        {
            return DeliveryEtaOperationalProfile.Default with { CalibrationSource = calibrationSource };
        }

        var totalMinutes = new List<double>();
        var acceptanceMinutes = new List<double>();
        var preparationMinutes = new List<double>();
        var dispatchLeadMinutes = new List<double>();
        var lastMileMinutes = new List<double>();

        foreach (var snapshot in snapshots)
        {
            var total = ClampMinutes(snapshot.DeliveredAtUtc - snapshot.PlacedAtUtc);
            if (total.HasValue)
            {
                totalMinutes.Add(total.Value);
            }

            if (snapshot.AcceptedAtUtc.HasValue)
            {
                var acceptance = ClampMinutes(snapshot.AcceptedAtUtc.Value - snapshot.PlacedAtUtc);
                if (acceptance.HasValue)
                {
                    acceptanceMinutes.Add(acceptance.Value);
                }
            }

            var prepStart = snapshot.PreparingAtUtc ?? snapshot.AcceptedAtUtc ?? snapshot.PlacedAtUtc;
            var prepEnd = snapshot.ReadyForPickupAtUtc
                ?? snapshot.DriverAssignedAtUtc
                ?? snapshot.AssignmentAcceptedAtUtc
                ?? snapshot.PickedUpAtUtc
                ?? snapshot.AssignmentPickedUpAtUtc
                ?? snapshot.DeliveredAtUtc;
            var prep = ClampMinutes(prepEnd - prepStart);
            if (prep.HasValue)
            {
                preparationMinutes.Add(prep.Value);
            }

            var dispatchStart = snapshot.ReadyForPickupAtUtc
                ?? snapshot.DriverAssignedAtUtc
                ?? snapshot.AssignmentAcceptedAtUtc;
            var pickupAt = snapshot.AssignmentPickedUpAtUtc ?? snapshot.PickedUpAtUtc;
            if (dispatchStart.HasValue && pickupAt.HasValue)
            {
                var dispatchLead = ClampMinutes(pickupAt.Value - dispatchStart.Value);
                if (dispatchLead.HasValue)
                {
                    dispatchLeadMinutes.Add(dispatchLead.Value);
                }
            }

            if (pickupAt.HasValue)
            {
                var lastMile = ClampMinutes(snapshot.DeliveredAtUtc - pickupAt.Value);
                if (lastMile.HasValue)
                {
                    lastMileMinutes.Add(lastMile.Value);
                }
            }
        }

        var averageTotal = AverageOrDefault(totalMinutes, DefaultAverageTotalMinutes);
        var averageAcceptance = AverageOrDefault(acceptanceMinutes, Math.Clamp(averageTotal * 0.10d, 3d, 12d));
        var averagePreparation = AverageOrDefault(preparationMinutes, Math.Clamp(averageTotal * 0.45, 12d, 35d));
        var averageDispatch = AverageOrDefault(dispatchLeadMinutes, Math.Clamp(averageTotal * 0.18, 5d, 20d));
        var averageLastMile = AverageOrDefault(lastMileMinutes, Math.Clamp(averageTotal * 0.28, 8d, 30d));
        var recommendedBuffer = ResolveRecommendedBuffer(totalMinutes, averageTotal);
        var percentile50 = PercentileOrDefault(totalMinutes, 0.50d, averageTotal);
        var percentile80 = PercentileOrDefault(totalMinutes, 0.80d, Math.Max(averageTotal + recommendedBuffer, averageTotal));
        var stageCoverageRatio = snapshots.Count == 0
            ? 0m
            : Math.Round(
                (
                    CoverageRatio(acceptanceMinutes.Count, snapshots.Count) +
                    CoverageRatio(preparationMinutes.Count, snapshots.Count) +
                    CoverageRatio(dispatchLeadMinutes.Count, snapshots.Count) +
                    CoverageRatio(lastMileMinutes.Count, snapshots.Count)) / 4m,
                2);
        var onTimeRate = totalMinutes.Count == 0
            ? 0m
            : Math.Round((decimal)totalMinutes.Count(minutes => minutes <= averageTotal + recommendedBuffer) / totalMinutes.Count * 100m, 1);

        return new DeliveryEtaOperationalProfile(
            CalibrationSource: calibrationSource,
            SampleSize: snapshots.Count,
            AverageTotalMinutes: averageTotal,
            AverageAcceptanceMinutes: averageAcceptance,
            AveragePreparationMinutes: averagePreparation,
            AverageDispatchLeadMinutes: averageDispatch,
            AverageLastMileMinutes: averageLastMile,
            Percentile50TotalMinutes: percentile50,
            Percentile80TotalMinutes: percentile80,
            RecommendedBufferMinutes: recommendedBuffer,
            OnTimeRate: onTimeRate,
            StageCoverageRatio: stageCoverageRatio,
            IsReliable: snapshots.Count >= MinimumReliableSampleSize && stageCoverageRatio >= 0.55m);
    }

    private static DeliveryEtaOperationalProfile ResolveBestProfile(
        IReadOnlyCollection<DeliveryEtaHistoricalSnapshot> snapshots,
        string calibrationSource,
        DateTime referenceUtc)
    {
        if (snapshots.Count == 0)
        {
            return DeliveryEtaOperationalProfile.Default with { CalibrationSource = calibrationSource };
        }

        var sameDayAndBucket = snapshots
            .Where(snapshot => snapshot.PlacedAtUtc.DayOfWeek == referenceUtc.DayOfWeek && GetHourBucket(snapshot.PlacedAtUtc) == GetHourBucket(referenceUtc))
            .ToArray();
        if (sameDayAndBucket.Length >= MinimumBucketSampleSize)
        {
            return BuildOperationalProfile(sameDayAndBucket, calibrationSource);
        }

        var sameHourBucket = snapshots
            .Where(snapshot => GetHourBucket(snapshot.PlacedAtUtc) == GetHourBucket(referenceUtc))
            .ToArray();
        if (sameHourBucket.Length >= MinimumBucketSampleSize)
        {
            return BuildOperationalProfile(sameHourBucket, calibrationSource);
        }

        var sameDay = snapshots
            .Where(snapshot => snapshot.PlacedAtUtc.DayOfWeek == referenceUtc.DayOfWeek)
            .ToArray();
        if (sameDay.Length >= MinimumBucketSampleSize)
        {
            return BuildOperationalProfile(sameDay, calibrationSource);
        }

        return BuildOperationalProfile(snapshots, calibrationSource);
    }

    private static async Task<IReadOnlyCollection<DeliveryEtaHistoricalSnapshot>> LoadSnapshotsAsync(
        IApplicationDbContext context,
        Guid vendorId,
        Guid? vendorBranchId,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var orders = await context.Orders
            .AsNoTracking()
            .Where(order =>
                order.VendorId == vendorId &&
                order.Status == OrderStatus.Delivered &&
                order.DeliveredAtUtc.HasValue &&
                order.PlacedAtUtc >= cutoff &&
                (!vendorBranchId.HasValue || order.VendorBranchId == vendorBranchId.Value))
            .OrderByDescending(order => order.DeliveredAtUtc)
            .Take(RecentHistoricalSampleLimit)
            .Select(order => new
            {
                order.Id,
                order.PlacedAtUtc,
                DeliveredAtUtc = order.DeliveredAtUtc!.Value,
                PreparingAtUtc = order.StatusHistory
                    .Where(history => history.NewStatus == OrderStatus.Preparing)
                    .Select(history => (DateTime?)history.CreatedAtUtc)
                    .Min(),
                AcceptedAtUtc = order.StatusHistory
                    .Where(history => history.NewStatus == OrderStatus.Accepted)
                    .Select(history => (DateTime?)history.CreatedAtUtc)
                    .Min(),
                ReadyForPickupAtUtc = order.StatusHistory
                    .Where(history => history.NewStatus == OrderStatus.ReadyForPickup)
                    .Select(history => (DateTime?)history.CreatedAtUtc)
                    .Min(),
                DriverAssignedAtUtc = order.StatusHistory
                    .Where(history =>
                        history.NewStatus == OrderStatus.DriverAssignmentInProgress ||
                        history.NewStatus == OrderStatus.DriverAssigned)
                    .Select(history => (DateTime?)history.CreatedAtUtc)
                    .Min(),
                PickedUpAtUtc = order.StatusHistory
                    .Where(history => history.NewStatus == OrderStatus.PickedUp)
                    .Select(history => (DateTime?)history.CreatedAtUtc)
                    .Min()
            })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Select(order => order.Id).ToArray();
        var assignments = await context.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment => orderIds.Contains(assignment.OrderId))
            .Select(assignment => new
            {
                assignment.OrderId,
                assignment.CreatedAtUtc,
                assignment.AcceptedAtUtc,
                assignment.PickedUpAtUtc
            })
            .ToListAsync(cancellationToken);

        var assignmentLookup = assignments
            .GroupBy(assignment => assignment.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(assignment => assignment.AcceptedAtUtc ?? assignment.CreatedAtUtc)
                    .First());

        return orders
            .Select(order =>
            {
                assignmentLookup.TryGetValue(order.Id, out var assignment);
                return new DeliveryEtaHistoricalSnapshot(
                    order.Id,
                    order.PlacedAtUtc,
                    order.DeliveredAtUtc,
                    order.AcceptedAtUtc,
                    order.PreparingAtUtc,
                    order.ReadyForPickupAtUtc,
                    order.DriverAssignedAtUtc,
                    order.PickedUpAtUtc,
                    assignment?.AcceptedAtUtc,
                    assignment?.PickedUpAtUtc);
            })
            .ToArray();
    }

    private static async Task<DeliveryEtaOperationalProfile> LoadRegionalFallbackProfileAsync(
        IApplicationDbContext context,
        string? customerCity,
        string? customerArea,
        DateTime referenceUtc,
        CancellationToken cancellationToken)
    {
        var normalizedArea = NormalizeKey(customerArea);
        var normalizedCity = NormalizeKey(customerCity);
        if (string.IsNullOrWhiteSpace(normalizedArea) && string.IsNullOrWhiteSpace(normalizedCity))
        {
            return DeliveryEtaOperationalProfile.Default with { CalibrationSource = "distance_baseline" };
        }

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var regionalOrders = await context.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.Delivered &&
                order.DeliveredAtUtc.HasValue &&
                order.PlacedAtUtc >= cutoff)
            .Join(
                context.CustomerAddresses.AsNoTracking(),
                order => order.CustomerAddressId,
                address => address.Id,
                (order, address) => new
                {
                    order.Id,
                    order.PlacedAtUtc,
                    DeliveredAtUtc = order.DeliveredAtUtc!.Value,
                    address.City,
                    address.Area,
                    PreparingAtUtc = order.StatusHistory
                        .Where(history => history.NewStatus == OrderStatus.Preparing)
                        .Select(history => (DateTime?)history.CreatedAtUtc)
                        .Min(),
                    AcceptedAtUtc = order.StatusHistory
                        .Where(history => history.NewStatus == OrderStatus.Accepted)
                        .Select(history => (DateTime?)history.CreatedAtUtc)
                        .Min(),
                    ReadyForPickupAtUtc = order.StatusHistory
                        .Where(history => history.NewStatus == OrderStatus.ReadyForPickup)
                        .Select(history => (DateTime?)history.CreatedAtUtc)
                        .Min(),
                    DriverAssignedAtUtc = order.StatusHistory
                        .Where(history =>
                            history.NewStatus == OrderStatus.DriverAssignmentInProgress ||
                            history.NewStatus == OrderStatus.DriverAssigned)
                        .Select(history => (DateTime?)history.CreatedAtUtc)
                        .Min(),
                    PickedUpAtUtc = order.StatusHistory
                        .Where(history => history.NewStatus == OrderStatus.PickedUp)
                        .Select(history => (DateTime?)history.CreatedAtUtc)
                        .Min()
                })
            .OrderByDescending(order => order.DeliveredAtUtc)
            .Take(RecentHistoricalSampleLimit * 3)
            .ToListAsync(cancellationToken);

        var matchedOrders = regionalOrders
            .Where(order => MatchesRegion(order.City, order.Area, normalizedCity, normalizedArea))
            .Take(RecentHistoricalSampleLimit)
            .ToList();
        if (matchedOrders.Count == 0)
        {
            return DeliveryEtaOperationalProfile.Default with { CalibrationSource = "distance_baseline" };
        }

        var orderIds = matchedOrders.Select(order => order.Id).ToArray();
        var assignments = await context.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment => orderIds.Contains(assignment.OrderId))
            .Select(assignment => new
            {
                assignment.OrderId,
                assignment.CreatedAtUtc,
                assignment.AcceptedAtUtc,
                assignment.PickedUpAtUtc
            })
            .ToListAsync(cancellationToken);

        var assignmentLookup = assignments
            .GroupBy(assignment => assignment.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(assignment => assignment.AcceptedAtUtc ?? assignment.CreatedAtUtc)
                    .First());

        var snapshots = matchedOrders
            .Select(order =>
            {
                assignmentLookup.TryGetValue(order.Id, out var assignment);
                return new DeliveryEtaHistoricalSnapshot(
                    order.Id,
                    order.PlacedAtUtc,
                    order.DeliveredAtUtc,
                    order.AcceptedAtUtc,
                    order.PreparingAtUtc,
                    order.ReadyForPickupAtUtc,
                    order.DriverAssignedAtUtc,
                    order.PickedUpAtUtc,
                    assignment?.AcceptedAtUtc,
                    assignment?.PickedUpAtUtc);
            })
            .ToArray();

        var profile = ResolveBestProfile(snapshots, "regional_fallback", referenceUtc);
        return profile.SampleSize > 0
            ? profile
            : DeliveryEtaOperationalProfile.Default with { CalibrationSource = "distance_baseline" };
    }

    private static double AverageOrDefault(List<double> values, double fallback) =>
        values.Count == 0 ? fallback : Math.Clamp(values.Average(), 5d, 180d);

    private static int ResolveRecommendedBuffer(List<double> totalMinutes, double averageTotalMinutes)
    {
        if (totalMinutes.Count < 3)
        {
            return 12;
        }

        var percentile80 = PercentileOrDefault(totalMinutes, 0.80d, averageTotalMinutes + 10d);
        return Math.Clamp((int)Math.Round(percentile80 - averageTotalMinutes), 8, 20);
    }

    private static double? ClampMinutes(TimeSpan duration)
    {
        var minutes = duration.TotalMinutes;
        return minutes is > 1 and < 240 ? minutes : null;
    }

    private static double PercentileOrDefault(List<double> values, double percentile, double fallback)
    {
        if (values.Count == 0)
        {
            return fallback;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var percentileIndex = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        percentileIndex = Math.Clamp(percentileIndex, 0, sorted.Length - 1);
        return sorted[percentileIndex];
    }

    private static decimal CoverageRatio(int count, int totalCount) =>
        totalCount == 0 ? 0m : (decimal)count / totalCount;

    private static int GetHourBucket(DateTime utcTimestamp) => utcTimestamp.Hour / 4;

    private static bool MatchesRegion(string? orderCity, string? orderArea, string? normalizedCity, string? normalizedArea)
    {
        var regionalArea = NormalizeKey(orderArea);
        if (!string.IsNullOrWhiteSpace(normalizedArea) &&
            !string.IsNullOrWhiteSpace(regionalArea) &&
            string.Equals(regionalArea, normalizedArea, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var regionalCity = NormalizeKey(orderCity);
        return !string.IsNullOrWhiteSpace(normalizedCity) &&
               !string.IsNullOrWhiteSpace(regionalCity) &&
               string.Equals(regionalCity, normalizedCity, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();

    private static double CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double earthRadiusKm = 6371d;
        var dLat = ToRadians((double)(lat2 - lat1));
        var dLon = ToRadians((double)(lon2 - lon1));
        var startLat = ToRadians((double)lat1);
        var endLat = ToRadians((double)lat2);

        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(startLat) * Math.Cos(endLat) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(earthRadiusKm * c, 2, MidpointRounding.AwayFromZero);
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180d);
}

public sealed record DeliveryEtaHistoricalSnapshot(
    Guid OrderId,
    DateTime PlacedAtUtc,
    DateTime DeliveredAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? PreparingAtUtc,
    DateTime? ReadyForPickupAtUtc,
    DateTime? DriverAssignedAtUtc,
    DateTime? PickedUpAtUtc,
    DateTime? AssignmentAcceptedAtUtc,
    DateTime? AssignmentPickedUpAtUtc);

public sealed record DeliveryEtaOperationalProfile(
    string CalibrationSource,
    int SampleSize,
    double AverageTotalMinutes,
    double AverageAcceptanceMinutes,
    double AveragePreparationMinutes,
    double AverageDispatchLeadMinutes,
    double AverageLastMileMinutes,
    double Percentile50TotalMinutes,
    double Percentile80TotalMinutes,
    int RecommendedBufferMinutes,
    decimal OnTimeRate,
    decimal StageCoverageRatio,
    bool IsReliable)
{
    public static DeliveryEtaOperationalProfile Default { get; } = new(
        CalibrationSource: "distance_baseline",
        SampleSize: 0,
        AverageTotalMinutes: 45d,
        AverageAcceptanceMinutes: 5d,
        AveragePreparationMinutes: 20d,
        AverageDispatchLeadMinutes: 8d,
        AverageLastMileMinutes: 15d,
        Percentile50TotalMinutes: 45d,
        Percentile80TotalMinutes: 60d,
        RecommendedBufferMinutes: 12,
        OnTimeRate: 0m,
        StageCoverageRatio: 0m,
        IsReliable: false);
}

public sealed record DeliveryEtaLiveSignal(
    int NearbyAvailableDrivers,
    double NearestAvailableDriverKm,
    bool HasHealthyCoverage,
    bool IsConstrained)
{
    public static DeliveryEtaLiveSignal None { get; } = new(0, 0d, false, false);
}
