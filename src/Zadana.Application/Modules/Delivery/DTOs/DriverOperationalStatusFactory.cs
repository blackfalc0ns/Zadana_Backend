using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Delivery.DTOs;

public static class DriverOperationalStatusFactory
{
    public static DriverOperationalStatusDto Create(Driver driver) =>
        new(
            DriverId: driver.Id,
            GateStatus: ResolveGateStatus(driver),
            IsOperational: driver.CanReceiveOrders,
            CanReceiveOrders: driver.CanReceiveOrders,
            CanGoAvailable: driver.CanReceiveOrders,
            IsAvailable: driver.IsAvailable,
            VerificationStatus: driver.VerificationStatus.ToString(),
            AccountStatus: driver.Status.ToString(),
            ReviewedAtUtc: driver.ReviewedAtUtc,
            ReviewNote: driver.ReviewNote,
            SuspensionReason: driver.SuspensionReason,
            PrimaryZoneId: driver.PrimaryZoneId,
            ZoneName: driver.PrimaryZone is not null ? $"{driver.PrimaryZone.City} - {driver.PrimaryZone.Name}" : null,
            Message: ResolveMessage(driver));

    public static string ResolveGateStatus(Driver driver) =>
        driver.VerificationStatus switch
        {
            DriverVerificationStatus.NeedsDocuments => "NeedsDocuments",
            DriverVerificationStatus.UnderReview => "UnderReview",
            DriverVerificationStatus.Rejected => "Rejected",
            DriverVerificationStatus.Approved when driver.Status == AccountStatus.Active => "Operational",
            DriverVerificationStatus.Approved when driver.Status == AccountStatus.Suspended => "Suspended",
            DriverVerificationStatus.Approved when driver.Status == AccountStatus.Banned => "Banned",
            DriverVerificationStatus.Approved when driver.Status == AccountStatus.Pending => "PendingActivation",
            DriverVerificationStatus.Approved when driver.Status == AccountStatus.Inactive => "Inactive",
            _ => "Unavailable"
        };

    public static string ResolveMessage(Driver driver) =>
        ResolveGateStatus(driver) switch
        {
            "NeedsDocuments" => "Driver profile is waiting for required documents before review.",
            "UnderReview" => "Driver profile is currently under admin review.",
            "Rejected" => "Driver profile was rejected by admin.",
            "Suspended" => "Driver account is suspended.",
            "Banned" => "Driver account is banned.",
            "Operational" => "Driver is approved and can receive orders.",
            "PendingActivation" => "Driver is approved but the account is still pending activation.",
            "Inactive" => "Driver is approved but the account is not currently active.",
            _ => "Driver operational status is unavailable."
        };
}
