using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Modules.Wallets.Support;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Tests.Application.Wallets;

public class DriverWalletNotificationComposerTests
{
    [Fact]
    public void ComposeWithdrawalSubmitted_IncludesWithdrawalIdAndEventName()
    {
        var withdrawal = CreateWithdrawal();

        var content = DriverWalletNotificationComposer.ComposeWithdrawalSubmitted(withdrawal);

        content.EventName.Should().Be("wallet.withdrawal_submitted");
        content.TitleAr.Should().Be("استلمنا طلب السحب");
        content.TitleEn.Should().Be("Withdrawal request submitted");
        AssertWithdrawalPayload(content.Data, withdrawal.Id, "wallet.withdrawal_submitted");
    }

    [Fact]
    public void ComposeWithdrawalCancelled_UsesCancelledEvent()
    {
        var withdrawal = CreateWithdrawal();
        withdrawal.Cancel("Cancelled by driver.");

        var content = DriverWalletNotificationComposer.ComposeWithdrawalCancelled(withdrawal);

        content.EventName.Should().Be("wallet.withdrawal_cancelled");
        content.BodyEn.Should().Contain(withdrawal.Id.ToString());
        AssertWithdrawalPayload(content.Data, withdrawal.Id, "wallet.withdrawal_cancelled");
    }

    [Fact]
    public void ComposeWithdrawalReturned_IncludesWithdrawalIdAndReason()
    {
        var withdrawal = CreateWithdrawal();
        withdrawal.MarkProcessing();
        withdrawal.MarkPaid("BANK-REF-001");
        withdrawal.MarkReturned("Beneficiary account rejected the transfer.");

        var content = DriverWalletNotificationComposer.ComposeWithdrawalReturned(
            withdrawal,
            Guid.NewGuid(),
            40m,
            "Beneficiary account rejected the transfer.");

        content.EventName.Should().Be("wallet.withdrawal_returned");
        AssertWithdrawalPayload(content.Data, withdrawal.Id, "wallet.withdrawal_returned");

        using var document = JsonDocument.Parse(content.Data);
        var root = document.RootElement;
        root.GetProperty("reason").GetString().Should().Be("Beneficiary account rejected the transfer.");
    }

    private static DriverWithdrawalRequest CreateWithdrawal()
    {
        var driverId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var payoutMethodId = Guid.NewGuid();
        return new DriverWithdrawalRequest(
            driverId,
            walletId,
            payoutMethodId,
            40m,
            "mobile-request-notification-test");
    }

    private static void AssertWithdrawalPayload(string data, Guid withdrawalId, string eventName)
    {
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        root.GetProperty("screen").GetString().Should().Be("wallet");
        root.GetProperty("event").GetString().Should().Be(eventName);
        root.GetProperty("eventName").GetString().Should().Be(eventName);
        root.GetProperty("withdrawalId").GetGuid().Should().Be(withdrawalId);
    }
}
