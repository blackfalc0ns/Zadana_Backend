using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class DeliveryEtaTelemetry
{
    private const int MinimumReliableSampleSize = 5;
    private const int RecentHistoricalSampleLimit = 60;
    private const double DefaultAverageTotalMinutes = 45d;

    public static async Task<DeliveryEtaOperationalProfile> LoadOperationalProfileAsync(
        IApplicationDbContext context,
        Guid vendorId,
        Guid? vendorBranchId,
        CancellationToken cancellationToken)
    {
        if (vendorBranchId.HasValue)
        {
            var branchSnapshots = await LoadSnapshotsAsync(context, vendorId, vendorBranchId, cancellationToken);
            var branchProfile = BuildOperationalProfile(branchSnapshots, "branch_historical");
            if (branchProfile.IsReliable)
            {
                return branchProfile;
            }
        }

        var vendorSnapshots = await LoadSnapshotsAsync(context, vendorId, null, cancellationToken);
        var vendorProfile = BuildOperationalProfile(vendorSnapshots, "vendor_historical");
        return vendorProfile.IsReliable || vendorProfile.SampleSize > 0
            ? vendorProfile
            : DeliveryEtaOperationalProfile.Default;
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
        var averagePreparation = AverageOrDefault(preparationMinutes, Math.Clamp(averageTotal * 0.45, 12d, 35d));
        var averageDispatch = AverageOrDefault(dispatchLeadMinutes, Math.Clamp(averageTotal * 0.18, 5d, 20d));
        var averageLastMile = AverageOrDefault(lastMileMinutes, Math.Clamp(averageTotal * 0.28, 8d, 30d));
        var recommendedBuffer = ResolveRecommendedBuffer(totalMinutes, averageTotal);
        var onTimeRate = totalMinutes.Count == 0
            ? 0m
            : Math.Round((decimal)totalMinutes.Count(minutes => minutes <= averageTotal + recommendedBuffer) / totalMinutes.Count * 100m, 1);

        return new DeliveryEtaOperationalProfile(
            CalibrationSource: calibrationSource,
            SampleSize: snapshots.Count,
            AverageTotalMinutes: averageTotal,
            AveragePreparationMinutes: averagePreparation,
            AverageDispatchLeadMinutes: averageDispatch,
            AverageLastMileMinutes: averageLastMile,
            RecommendedBufferMinutes: recommendedBuffer,
            OnTimeRate: onTimeRate,
            IsReliable: snapshots.Count >= MinimumReliableSampleSize);
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

    private static double AverageOrDefault(List<double> values, double fallback) =>
        values.Count == 0 ? fallback : Math.Clamp(values.Average(), 5d, 180d);

    private static int ResolveRecommendedBuffer(List<double> totalMinutes, double averageTotalMinutes)
    {
        if (totalMinutes.Count < 3)
        {
            return 12;
        }

        var sorted = totalMinutes.OrderBy(value => value).ToArray();
        var percentileIndex = (int)Math.Ceiling(sorted.Length * 0.75) - 1;
        percentileIndex = Math.Clamp(percentileIndex, 0, sorted.Length - 1);
        var percentile75 = sorted[percentileIndex];
        return Math.Clamp((int)Math.Round(percentile75 - averageTotalMinutes), 8, 20);
    }

    private static double? ClampMinutes(TimeSpan duration)
    {
        var minutes = duration.TotalMinutes;
        return minutes is > 1 and < 240 ? minutes : null;
    }
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
    double AveragePreparationMinutes,
    double AverageDispatchLeadMinutes,
    double AverageLastMileMinutes,
    int RecommendedBufferMinutes,
    decimal OnTimeRate,
    bool IsReliable)
{
    public static DeliveryEtaOperationalProfile Default { get; } = new(
        CalibrationSource: "default_policy",
        SampleSize: 0,
        AverageTotalMinutes: 45d,
        AveragePreparationMinutes: 20d,
        AverageDispatchLeadMinutes: 8d,
        AverageLastMileMinutes: 15d,
        RecommendedBufferMinutes: 12,
        OnTimeRate: 0m,
        IsReliable: false);
}
