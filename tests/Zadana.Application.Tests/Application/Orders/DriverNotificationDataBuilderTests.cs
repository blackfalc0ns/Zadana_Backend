using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverNotificationDataBuilderTests
{
    [Fact]
    public void Build_ForWalletEvent_ShouldIncludePopupContractAndTargetUrl()
    {
        var withdrawalId = Guid.NewGuid();

        var json = DriverNotificationDataBuilder.Build(
            screen: "wallet",
            @event: "wallet.withdrawal_submitted",
            withdrawalId: withdrawalId,
            extra: new
            {
                amount = 120m
            });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("presentation").GetString().Should().Be("popup");
        root.GetProperty("popupType").GetString().Should().Be("driver_wallet_updated");
        root.GetProperty("showPopup").GetBoolean().Should().BeTrue();
        root.GetProperty("eventName").GetString().Should().Be("wallet.withdrawal_submitted");
        root.GetProperty("targetUrl").GetString().Should().Be($"/wallet/withdrawals/{withdrawalId}");
        root.GetProperty("category").GetString().Should().Be("wallet");
    }

    [Fact]
    public void Build_ForDeliveryOffer_ShouldKeepDeliveryOfferPopupType()
    {
        var assignmentId = Guid.NewGuid();

        var json = DriverNotificationDataBuilder.Build(
            screen: "home",
            @event: "dispatch.offer_new",
            assignmentId: assignmentId);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("presentation").GetString().Should().Be("popup");
        root.GetProperty("popupType").GetString().Should().Be("delivery_offer");
        root.GetProperty("showPopup").GetBoolean().Should().BeTrue();
        root.GetProperty("targetUrl").GetString().Should().Be("/");
        root.GetProperty("category").GetString().Should().Be("dispatch");
    }
}
