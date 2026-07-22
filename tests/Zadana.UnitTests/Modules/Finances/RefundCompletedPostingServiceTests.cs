using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finances;

public class RefundCompletedPostingServiceTests
{
    [Fact]
    public async Task PostAsync_WhenVendorBearsRefund_ShouldDebitVendorPayableAndCreditGateway()
    {
        await using var context = TestDbContextFactory.Create();
        var postingService = new FinancialEventPostingService(
            context,
            Mock.Of<ILogger<FinancialEventPostingService>>());
        var projectionUpdater = new WalletProjectionUpdater(context);
        var service = new RefundCompletedPostingService(
            postingService,
            projectionUpdater,
            Mock.Of<ILogger<RefundCompletedPostingService>>(),
            Options.Create(new FinancialSettingsOptions()));

        var vendorId = Guid.NewGuid();
        var order = CreateOrder(vendorId, PaymentMethodType.Card, 100m);
        var payment = new Payment(order.Id, PaymentMethodType.Card, 100m);
        var refund = new Refund(payment.Id, 100m, "Customer return", costBearer: "vendor");
        refund.Process();

        var allocation = new RefundAllocation(
            refund.Id,
            80m,
            10m,
            10m,
            0m,
            0m,
            100m,
            0m);

        await service.PostAsync(refund, order, payment, allocation, driverId: null, CancellationToken.None);

        context.JournalLines
            .Where(line => line.AccountCode == FinancialAccountCode.RefundExpense)
            .Sum(line => line.DebitAmount - line.CreditAmount)
            .Should()
            .Be(0m);

        context.JournalLines
            .Where(line => line.AccountCode == FinancialAccountCode.VendorPayable && line.OwnerId == vendorId)
            .Sum(line => line.DebitAmount - line.CreditAmount)
            .Should()
            .Be(100m);

        context.JournalLines
            .Where(line => line.AccountCode == FinancialAccountCode.GatewayReceivable)
            .Sum(line => line.CreditAmount - line.DebitAmount)
            .Should()
            .Be(100m);

        var vendorWallet = context.Wallets.Single(wallet =>
            wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendorId);
        vendorWallet.CurrentBalance.Should().Be(-100m);
    }

    [Fact]
    public async Task PostAsync_WhenPlatformBearsRefund_ShouldPostRefundExpense()
    {
        await using var context = TestDbContextFactory.Create();
        var postingService = new FinancialEventPostingService(
            context,
            Mock.Of<ILogger<FinancialEventPostingService>>());
        var projectionUpdater = new WalletProjectionUpdater(context);
        var service = new RefundCompletedPostingService(
            postingService,
            projectionUpdater,
            Mock.Of<ILogger<RefundCompletedPostingService>>(),
            Options.Create(new FinancialSettingsOptions()));

        var vendorId = Guid.NewGuid();
        var order = CreateOrder(vendorId, PaymentMethodType.CashOnDelivery, 50m);
        var payment = new Payment(order.Id, PaymentMethodType.CashOnDelivery, 50m);
        var refund = new Refund(payment.Id, 50m, "Customer return", costBearer: "platform");
        refund.Process();

        var allocation = new RefundAllocation(
            refund.Id,
            40m,
            5m,
            5m,
            0m,
            50m,
            0m,
            0m);

        await service.PostAsync(refund, order, payment, allocation, driverId: null, CancellationToken.None);

        context.JournalLines
            .Where(line => line.AccountCode == FinancialAccountCode.RefundExpense)
            .Sum(line => line.DebitAmount - line.CreditAmount)
            .Should()
            .Be(50m);

        context.FinancialEvents
            .Single(item => item.EventType == FinancialEventType.RefundCompleted)
            .RefundId
            .Should()
            .Be(refund.Id);
    }

    private static Order CreateOrder(Guid vendorId, PaymentMethodType paymentMethod, decimal subtotal)
    {
        var order = new Order(
            "ORD-REF-1",
            Guid.NewGuid(),
            vendorId,
            Guid.NewGuid(),
            paymentMethod,
            subtotal,
            0m,
            5m,
            5m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            0m);

        order.ChangeStatus(OrderStatus.Delivered);
        return order;
    }
}
