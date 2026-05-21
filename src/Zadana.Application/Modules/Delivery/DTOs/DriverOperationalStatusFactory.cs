using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Delivery.DTOs;

public static class DriverOperationalStatusFactory
{
    public static DriverOperationalStatusDto Create(
        Driver driver,
        DriverCommitmentSummaryDto? commitment = null,
        bool isLoginLocked = false,
        DateTime? lockedAtUtc = null,
        string? lockReason = null)
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

        var gateStatus = ResolveGateStatus(driver, isLoginLocked);
        var gateMessageAr = ResolveMessageAr(driver, isLoginLocked);
        var gateMessageEn = ResolveMessageEn(driver, isLoginLocked);
        var messageAr = commitment.RestrictionMessage ?? gateMessageAr;
        var messageEn = commitment.RestrictionMessageEn ?? gateMessageEn;
        var canReceiveOrders = !isLoginLocked && driver.CanReceiveOrders;
        var canReceiveOffers = canReceiveOrders && commitment.CanReceiveOffers;
        var canEditProfile = !isLoginLocked && driver.Status != AccountStatus.Banned;

        return new DriverOperationalStatusDto(
            DriverId: driver.Id,
            GateStatus: gateStatus,
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
            RestrictionMessageEn: commitment.RestrictionMessageEn,
            ReviewNoteAr: ResolveReviewNoteAr(driver.ReviewNote),
            ReviewNoteEn: ResolveReviewNoteEn(driver.ReviewNote),
            IsLoginLocked: isLoginLocked,
            LockedAtUtc: lockedAtUtc,
            LockReason: lockReason,
            AllowedCapabilities: new DriverAllowedCapabilitiesDto(
                CanAccessSupport: true,
                CanEditProfile: canEditProfile,
                CanAccessWallet: canReceiveOrders,
                CanReceiveOffers: canReceiveOffers),
            SupportCta: new DriverSupportCtaDto(
                Endpoint: isLoginLocked ? "/api/drivers/account-support/appeals" : "/api/drivers/support/account-appeals",
                ReasonType: ResolveSupportReasonType(gateStatus),
                LabelAr: "تواصل مع دعم حساب المندوب",
                LabelEn: "Contact driver account support"));
    }

    public static string ResolveGateStatus(Driver driver, bool isLoginLocked = false) =>
        isLoginLocked
            ? "LoginLocked"
            : driver.HasExpiredRequiredDocuments()
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

    public static string ResolveMessageAr(Driver driver, bool isLoginLocked = false) =>
        ResolveGateStatus(driver, isLoginLocked) switch
        {
            "LoginLocked" => "تسجيل الدخول مقفل لهذا الحساب. يمكنك التواصل مع دعم حساب المندوب.",
            "NeedsDocuments" => "ملف المندوب يحتاج استكمال المستندات المطلوبة قبل المراجعة.",
            "UnderReview" => "ملف المندوب قيد مراجعة الإدارة حاليًا.",
            "Rejected" => "تم رفض ملف المندوب من الإدارة.",
            "ExpiredDocuments" => "حساب المندوب مغلق حتى يتم تجديد المستندات المنتهية.",
            "Suspended" => "حساب المندوب موقوف.",
            "Banned" => "حساب المندوب محظور.",
            "Operational" => "تم اعتماد المندوب ويمكنه استقبال الطلبات.",
            "PendingActivation" => "تم اعتماد المندوب لكن الحساب ما زال في انتظار التفعيل.",
            "Inactive" => "تم اعتماد المندوب لكن الحساب غير نشط حاليًا.",
            _ => "حالة تشغيل المندوب غير متاحة حاليًا."
        };

    public static string ResolveMessageEn(Driver driver, bool isLoginLocked = false) =>
        ResolveGateStatus(driver, isLoginLocked) switch
        {
            "LoginLocked" => "Login is locked for this account. You can contact driver account support.",
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

    public static string? ResolveReviewNoteAr(string? note) =>
        NormalizeReviewNote(note) switch
        {
            null => null,
            "profile updated and pending admin re-review" => "تم تحديث الملف وهو في انتظار إعادة مراجعة الإدارة.",
            "documents approved and pending final account approval" => "تم اعتماد المستندات والملف في انتظار الموافقة النهائية على الحساب.",
            "expired_required_documents" => "يوجد مستند مطلوب منتهي الصلاحية. يرجى تجديد المستندات.",
            "additional documents required" => "مطلوب استكمال مستندات إضافية.",
            "driver account approved" => "تم اعتماد حساب المندوب.",
            "driver application rejected" => "تم رفض طلب تسجيل المندوب.",
            _ => note
        };

    public static string? ResolveReviewNoteEn(string? note) =>
        NormalizeReviewNote(note) switch
        {
            null => null,
            "profile updated and pending admin re-review" => "Profile updated and pending admin re-review.",
            "documents approved and pending final account approval" => "Documents approved and pending final account approval.",
            "expired_required_documents" => "A required document has expired. Please renew the documents.",
            "additional documents required" => "Additional documents are required.",
            "driver account approved" => "Driver account approved.",
            "driver application rejected" => "Driver application rejected.",
            _ => note
        };

    private static string ResolveSupportReasonType(string gateStatus) =>
        gateStatus switch
        {
            "LoginLocked" => "login_locked",
            "Banned" => "account_banned",
            "Suspended" => "account_suspended",
            "UnderReview" => "under_review",
            "NeedsDocuments" or "ExpiredDocuments" => "documents_required",
            _ => "other"
        };

    private static string? NormalizeReviewNote(string? note) =>
        string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim().TrimEnd('.').ToLowerInvariant();
}
