using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Application.Modules.Wallets.Support;

public sealed record DriverWalletNotificationContent(
    string EventName,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string Data,
    string TargetUrl);

public static class DriverWalletNotificationComposer
{
    public static DriverWalletNotificationContent ComposeWithdrawalSubmitted(DriverWithdrawalRequest withdrawal)
    {
        var amount = FormatAmount(withdrawal.Amount);
        return ComposeWithdrawal(
            "wallet.withdrawal_submitted",
            "استلمنا طلب السحب",
            "Withdrawal request submitted",
            $"استلمنا طلب سحب بقيمة {amount}.",
            $"Your withdrawal request for {amount} was submitted.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString()
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalCancelled(DriverWithdrawalRequest withdrawal)
    {
        return ComposeWithdrawal(
            "wallet.withdrawal_cancelled",
            "ألغينا طلب السحب",
            "Withdrawal cancelled",
            $"ألغيت طلب السحب رقم #{withdrawal.Id} وأعدنا المبلغ إلى رصيد محفظتك.",
            $"Withdrawal request #{withdrawal.Id} was cancelled and the amount was restored to your wallet.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString()
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalProcessing(DriverWithdrawalRequest withdrawal)
    {
        return ComposeWithdrawal(
            "wallet.withdrawal_processing",
            "جاري تحويل السحب",
            "Withdrawal transfer started",
            $"جاري تحويل طلب السحب رقم #{withdrawal.Id}.",
            $"Your withdrawal request #{withdrawal.Id} is being transferred.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString(),
                payoutId = withdrawal.PayoutId
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalRejected(DriverWithdrawalRequest withdrawal)
    {
        return ComposeWithdrawal(
            "wallet.withdrawal_rejected",
            "رفضنا طلب السحب",
            "Withdrawal rejected",
            $"رفضنا طلب السحب رقم #{withdrawal.Id}.",
            $"Your withdrawal request #{withdrawal.Id} was rejected.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString(),
                failureReason = withdrawal.FailureReason
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalFailed(DriverWithdrawalRequest withdrawal)
    {
        return ComposeWithdrawal(
            "wallet.withdrawal_failed",
            "فشل تحويل السحب",
            "Withdrawal transfer failed",
            $"فشل تحويل طلب السحب رقم #{withdrawal.Id}. تواصل مع الدعم.",
            $"Your withdrawal request #{withdrawal.Id} transfer failed. Please contact support.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString(),
                failureReason = withdrawal.FailureReason
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalPaid(
        DriverWithdrawalRequest withdrawal,
        bool hasTransferProof = false)
    {
        return ComposeWithdrawal(
            "wallet.withdrawal_paid",
            "حوّلنا مبلغ السحب",
            "Withdrawal paid",
            $"حوّلنا طلب السحب رقم #{withdrawal.Id} بنجاح.",
            $"Your withdrawal request #{withdrawal.Id} was paid successfully.",
            withdrawal,
            extra: new
            {
                amount = withdrawal.Amount,
                status = withdrawal.Status.ToString(),
                transferReference = withdrawal.TransferReference,
                payoutId = withdrawal.PayoutId,
                hasTransferProof
            });
    }

    public static DriverWalletNotificationContent ComposeWithdrawalReturned(
        DriverWithdrawalRequest withdrawal,
        Guid payoutId,
        decimal amount,
        string? reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Bank transfer returned."
            : reason.Trim();

        return ComposeWithdrawal(
            "wallet.withdrawal_returned",
            "تم إرجاع الحوالة البنكية",
            "Bank transfer returned",
            "تعذر إيداع مبلغ السحب في الحساب البنكي وتمت إعادة المبلغ إلى رصيد محفظتك. راجع بيانات الحساب قبل تقديم طلب جديد.",
            "The withdrawal could not be deposited and the amount was restored to your wallet. Review your bank details before submitting a new request.",
            withdrawal,
            extra: new
            {
                payoutId,
                amount,
                status = withdrawal.Status.ToString(),
                reason = normalizedReason,
                failureReason = withdrawal.FailureReason
            });
    }

    public static DriverWalletNotificationContent ComposeAdminWalletAdjustment(
        Guid walletId,
        Guid transactionId,
        decimal amount,
        string direction)
    {
        var data = DriverNotificationDataBuilder.Build(
            screen: "wallet",
            @event: "wallet.admin_adjustment",
            extra: new
            {
                walletId,
                amount,
                direction,
                transactionId
            });

        return new DriverWalletNotificationContent(
            "wallet.admin_adjustment",
            "عدّلنا رصيد المحفظة",
            "Wallet balance adjusted",
            "عدّلنا رصيد محفظتك من الإدارة.",
            "Your wallet balance was adjusted by the team.",
            data,
            "/wallet");
    }

    private static DriverWalletNotificationContent ComposeWithdrawal(
        string eventName,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        DriverWithdrawalRequest withdrawal,
        object extra)
    {
        var data = BuildWithdrawalData(eventName, withdrawal, extra);
        return new DriverWalletNotificationContent(
            eventName,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            data,
            ResolveWithdrawalTargetUrl(withdrawal.Id));
    }

    private static string ResolveWithdrawalTargetUrl(Guid withdrawalId) =>
        $"/wallet/withdrawals/{withdrawalId:D}";

    private static string BuildWithdrawalData(
        string eventName,
        DriverWithdrawalRequest withdrawal,
        object extra)
    {
        return DriverNotificationDataBuilder.Build(
            screen: "wallet",
            @event: eventName,
            withdrawalId: withdrawal.Id,
            extra: extra);
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.##");
}
