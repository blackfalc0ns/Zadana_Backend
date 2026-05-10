using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Delivery.DTOs;

public static class DriverOperationalStatusFactory
{
    public static DriverOperationalStatusDto Create(
        Driver driver,
        DriverCommitmentSummaryDto? commitment = null)
    {
        commitment ??= new DriverCommitmentSummaryDto(
            AcceptedOffers: 0,
            RejectedOffers: 0,
            TimedOutOffers: 0,
            DailyRejections: 0,
            WeeklyRejections: 0,
            CommitmentScore: 100m,
            EnforcementLevel: DriverCommitmentEnforcementLevel.Healthy.ToString(),
            CanReceiveOffers: true,
            RestrictionMessage: null,
            LastOfferResponseAtUtc: null,
            RestrictionMessageEn: null);

        var gateMessageAr = ResolveMessageAr(driver);
        var gateMessageEn = ResolveMessageEn(driver);
        var messageAr = commitment.RestrictionMessage ?? gateMessageAr;
        var messageEn = commitment.RestrictionMessageEn ?? gateMessageEn;
        var canReceiveOrders = driver.CanReceiveOrders;
        var canReceiveOffers = canReceiveOrders && commitment.CanReceiveOffers;

        return
        new(
            DriverId: driver.Id,
            GateStatus: ResolveGateStatus(driver),
            IsOperational: canReceiveOffers,
            CanReceiveOrders: canReceiveOrders,
            CanGoAvailable: canReceiveOffers,
            IsAvailable: driver.IsAvailable,
            VerificationStatus: driver.VerificationStatus.ToString(),
            AccountStatus: driver.Status.ToString(),
            ReviewedAtUtc: driver.ReviewedAtUtc,
            ReviewNote: driver.ReviewNote,
            SuspensionReason: driver.SuspensionReason,
            CommitmentScore: commitment.CommitmentScore,
            DailyRejections: commitment.DailyRejections,
            WeeklyRejections: commitment.WeeklyRejections,
            EnforcementLevel: commitment.EnforcementLevel,
            CanReceiveOffers: canReceiveOffers,
            RestrictionMessage: commitment.RestrictionMessage,
            Message: messageAr,
            MessageAr: messageAr,
            MessageEn: messageEn,
            RestrictionMessageAr: commitment.RestrictionMessage,
            RestrictionMessageEn: commitment.RestrictionMessageEn);
    }

    public static string ResolveGateStatus(Driver driver) =>
        driver.HasExpiredRequiredDocuments()
            ? "ExpiredDocuments"
            : driver.VerificationStatus switch
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

    public static string ResolveMessageAr(Driver driver) =>
        ResolveGateStatus(driver) switch
        {
            "NeedsDocuments" => "ملف السائق ينتظر استكمال المستندات المطلوبة قبل المراجعة.",
            "UnderReview" => "ملف السائق قيد مراجعة الإدارة حاليًا.",
            "Rejected" => "تم رفض ملف السائق من الإدارة.",
            "ExpiredDocuments" => "حساب السائق مغلق حتى يتم تجديد المستندات المنتهية.",
            "Suspended" => "حساب السائق موقوف.",
            "Banned" => "حساب السائق محظور.",
            "Operational" => "تم اعتماد السائق ويمكنه استقبال الطلبات.",
            "PendingActivation" => "تم اعتماد السائق لكن الحساب ما زال في انتظار التفعيل.",
            "Inactive" => "تم اعتماد السائق لكن الحساب غير نشط حاليًا.",
            _ => "حالة تشغيل السائق غير متاحة حاليًا."
        };

    public static string ResolveMessageEn(Driver driver) =>
        ResolveGateStatus(driver) switch
        {
            "NeedsDocuments" => "Driver profile is waiting for required documents before review.",
            "UnderReview" => "Driver profile is currently under admin review.",
            "Rejected" => "Driver profile was rejected by admin.",
            "ExpiredDocuments" => "Driver account is closed until expired documents are renewed.",
            "Suspended" => "Driver account is suspended.",
            "Banned" => "Driver account is banned.",
            "Operational" => "Driver is approved and can receive orders.",
            "PendingActivation" => "Driver is approved but the account is still pending activation.",
            "Inactive" => "Driver is approved but the account is not currently active.",
            _ => "Driver operational status is unavailable."
        };
}
