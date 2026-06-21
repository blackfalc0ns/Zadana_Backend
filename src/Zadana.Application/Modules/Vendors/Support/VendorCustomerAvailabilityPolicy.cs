using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorCustomerAvailabilityPolicy
{
    public const string StoreAvailabilityFeature = "store-availability";
    public const string VendorOfflineReason = "vendor_offline";
    public const string OutsideWorkingHoursReason = "outside_working_hours";
    public const string AcceptOrdersDisabledReason = "accept_orders_disabled";
    public const string VendorInactiveReason = "vendor_inactive";

    public static async Task<IReadOnlyDictionary<Guid, VendorCustomerAvailabilityDecision>> LoadDecisionsAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> vendorIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = vendorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requestedIds.Length == 0)
        {
            return new Dictionary<Guid, VendorCustomerAvailabilityDecision>();
        }

        var vendors = await context.Vendors
            .AsNoTracking()
            .Where(vendor => requestedIds.Contains(vendor.Id))
            .Select(vendor => new VendorSnapshot(
                vendor.Id,
                vendor.Status,
                vendor.AcceptOrders,
                vendor.CommercialRegistrationExpiryDate))
            .ToListAsync(cancellationToken);

        var workspaceStates = await context.VendorWorkspaceStates
            .AsNoTracking()
            .Where(state =>
                requestedIds.Contains(state.VendorId) &&
                state.Feature == VendorWorkspaceState.NormalizeFeature(StoreAvailabilityFeature))
            .Select(state => new WorkspaceStateSnapshot(state.VendorId, state.PayloadJson))
            .ToListAsync(cancellationToken);

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Where(branch => requestedIds.Contains(branch.VendorId))
            .Select(branch => new BranchSnapshot(branch.Id, branch.VendorId, branch.IsActive, branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var primaryBranchIds = branches
            .GroupBy(branch => branch.VendorId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(branch => branch.IsActive)
                    .ThenBy(branch => branch.CreatedAtUtc)
                    .Select(branch => branch.Id)
                    .FirstOrDefault());

        var branchIds = primaryBranchIds.Values
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var operatingHours = branchIds.Length == 0
            ? new List<BranchOperatingHourSnapshot>()
            : await context.BranchOperatingHours
                .AsNoTracking()
                .Where(hour => branchIds.Contains(hour.BranchId))
                .Select(hour => new BranchOperatingHourSnapshot(
                    hour.BranchId,
                    hour.DayOfWeek,
                    hour.OpenTime,
                    hour.CloseTime,
                    hour.IsClosed))
                .ToListAsync(cancellationToken);

        var hoursByBranchId = operatingHours
            .GroupBy(hour => hour.BranchId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var manualModeByVendorId = workspaceStates.ToDictionary(
            item => item.VendorId,
            item => ParseManualMode(item.PayloadJson));

        var localNow = GetLocalNow();
        var decisions = new Dictionary<Guid, VendorCustomerAvailabilityDecision>(vendors.Count);

        foreach (var vendor in vendors)
        {
            primaryBranchIds.TryGetValue(vendor.Id, out var primaryBranchId);
            var withinWorkingHours = IsWithinWorkingHours(
                primaryBranchId,
                hoursByBranchId,
                localNow);

            manualModeByVendorId.TryGetValue(vendor.Id, out var manualMode);
            decisions[vendor.Id] = BuildDecision(vendor, manualMode, withinWorkingHours);
        }

        return decisions;
    }

    public static VendorCustomerAvailabilityDecision ResolveOrOffline(
        IReadOnlyDictionary<Guid, VendorCustomerAvailabilityDecision> decisions,
        Guid vendorId)
    {
        return decisions.TryGetValue(vendorId, out var decision)
            ? decision
            : new VendorCustomerAvailabilityDecision(
                false,
                false,
                false,
                VendorInactiveReason,
                "Vendor is not available.");
    }

    private static VendorCustomerAvailabilityDecision BuildDecision(
        VendorSnapshot vendor,
        string? manualMode,
        bool withinWorkingHours)
    {
        if (vendor.Status != VendorStatus.Active)
        {
            return new VendorCustomerAvailabilityDecision(false, false, false, VendorInactiveReason, "Vendor is inactive.");
        }

        if (vendor.CommercialRegistrationExpiryDate.HasValue && vendor.CommercialRegistrationExpiryDate.Value.Date < SaudiTime.Today)
        {
            return new VendorCustomerAvailabilityDecision(false, false, false, "documents_expired", "Vendor registration documents have expired.");
        }

        if (string.Equals(manualMode, "offline", StringComparison.OrdinalIgnoreCase))
        {
            return new VendorCustomerAvailabilityDecision(false, false, false, VendorOfflineReason, "Vendor is temporarily unavailable.");
        }

        if (!withinWorkingHours)
        {
            return new VendorCustomerAvailabilityDecision(false, false, false, OutsideWorkingHoursReason, "Vendor is outside working hours.");
        }

        if (!vendor.AcceptOrders)
        {
            return new VendorCustomerAvailabilityDecision(false, false, false, AcceptOrdersDisabledReason, "Vendor is not accepting orders right now.");
        }

        return new VendorCustomerAvailabilityDecision(true, true, true, null, null);
    }

    private static bool IsWithinWorkingHours(
        Guid primaryBranchId,
        IReadOnlyDictionary<Guid, BranchOperatingHourSnapshot[]> hoursByBranchId,
        DateTime localNow)
    {
        if (primaryBranchId == Guid.Empty || !hoursByBranchId.TryGetValue(primaryBranchId, out var hours) || hours.Length == 0)
        {
            return true;
        }

        var currentDay = (int)localNow.DayOfWeek;
        var currentTime = localNow.TimeOfDay;

        foreach (var hour in hours)
        {
            if (hour.IsClosed)
            {
                continue;
            }

            if (hour.CloseTime >= hour.OpenTime)
            {
                if (hour.DayOfWeek == currentDay && currentTime >= hour.OpenTime && currentTime <= hour.CloseTime)
                {
                    return true;
                }

                continue;
            }

            if (hour.DayOfWeek == currentDay && currentTime >= hour.OpenTime)
            {
                return true;
            }

            var nextDay = ((hour.DayOfWeek + 1) % 7);
            if (nextDay == currentDay && currentTime <= hour.CloseTime)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ParseManualMode(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return document.RootElement.TryGetProperty("manual_mode", out var manualMode)
                ? manualMode.GetString()?.Trim().ToLowerInvariant()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime GetLocalNow()
    {
        foreach (var timezoneId in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try
            {
                var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return DateTime.UtcNow.AddHours(3);
    }

    private sealed record VendorSnapshot(Guid Id, VendorStatus Status, bool AcceptOrders, DateTime? CommercialRegistrationExpiryDate);

    private sealed record WorkspaceStateSnapshot(Guid VendorId, string PayloadJson);

    private sealed record BranchSnapshot(Guid Id, Guid VendorId, bool IsActive, DateTime CreatedAtUtc);

    private sealed record BranchOperatingHourSnapshot(
        Guid BranchId,
        int DayOfWeek,
        TimeSpan OpenTime,
        TimeSpan CloseTime,
        bool IsClosed);
}

public sealed record VendorCustomerAvailabilityDecision(
    bool IsVisibleInCatalog,
    bool IsPurchasable,
    bool IsOnlineNow,
    string? ReasonCode,
    string? ReasonMessage);
