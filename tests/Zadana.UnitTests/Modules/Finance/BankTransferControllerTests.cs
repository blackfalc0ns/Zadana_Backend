using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Api.Modules.Payments.Controllers;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class BankTransferControllerTests
{
    [Fact]
    public async Task ConfirmBankTransfer_WhenPaymentAlreadyPaidButLedgerMissing_ShouldPostMissingLedgerOnce()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateBankTransferOrder();
        var payment = new Payment(order.Id, PaymentMethodType.BankTransfer, order.TotalAmount);
        payment.MarkAsPaid("BANK-REF-1");
        order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Bank transfer confirmed by previous attempt");
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        context.Orders.Add(order);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var firstResult = await controller.ConfirmBankTransfer(payment.Id, null, CancellationToken.None);
        var secondResult = await controller.ConfirmBankTransfer(payment.Id, null, CancellationToken.None);

        firstResult.Should().BeOfType<OkObjectResult>();
        secondResult.Should().BeOfType<OkObjectResult>();
        context.FinancialEvents.Count(item => item.EventType == FinancialEventType.BankTransferConfirmed).Should().Be(1);

        var entry = context.JournalEntries.Single(item => item.FinancialEvent.EventType == FinancialEventType.BankTransferConfirmed);
        entry.Lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.CustomerAdvance &&
            line.CreditAmount == order.TotalAmount &&
            line.OwnerId == order.UserId);
    }

    [Fact]
    public async Task ConfirmBankTransfer_ShouldUseConfiguredPlatformWalletOwner()
    {
        await using var context = TestDbContextFactory.Create();
        var platformOwnerId = Guid.NewGuid();
        var order = CreateBankTransferOrder();
        var payment = new Payment(order.Id, PaymentMethodType.BankTransfer, order.TotalAmount);
        payment.MarkAsPaid("BANK-REF-2");
        order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Bank transfer confirmed by previous attempt");
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        context.Orders.Add(order);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var controller = CreateController(context, platformOwnerId);
        await controller.ConfirmBankTransfer(payment.Id, null, CancellationToken.None);

        var entry = context.JournalEntries.Single(item => item.FinancialEvent.EventType == FinancialEventType.BankTransferConfirmed);
        entry.Lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.PlatformCash &&
            line.OwnerId == platformOwnerId);
    }

    private static BankTransferController CreateController(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        Guid? platformOwnerId = null)
    {
        var postingService = new FinancialEventPostingService(
            context,
            Mock.Of<ILogger<FinancialEventPostingService>>());

        return new BankTransferController(
            context,
            postingService,
            new WalletProjectionUpdater(context),
            Mock.Of<IPublisher>(),
            Mock.Of<IEmailCenterService>(),
            Options.Create(new BankTransferSettingsOptions()),
            Options.Create(new FinancialSettingsOptions
            {
                PlatformWalletOwnerId = platformOwnerId ?? Guid.Parse("00000000-0000-0000-0000-000000000001")
            }),
            Mock.Of<IWebHostEnvironment>(),
            NullLogger<BankTransferController>.Instance);
    }

    private static Order CreateBankTransferOrder() =>
        new(
            "ORD-BANK-HEAL",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PaymentMethodType.BankTransfer,
            subtotal: 100m,
            discountTotal: 0m,
            deliveryFee: 10m,
            baseDeliveryFee: 10m,
            distanceDeliveryFee: 0m,
            surgeDeliveryFee: 0m,
            quotedDistanceKm: null,
            deliveryPricingMode: "live",
            deliveryPricingRuleLabel: null,
            driverToVendorDistanceKm: 0m,
            vendorToCustomerDistanceKm: 0m,
            driverToVendorFee: 0m,
            vendorToCustomerFee: 0m,
            driverToVendorPricingSource: null,
            vendorToCustomerPricingSource: null,
            usedEstimatedDriverPricing: false,
            pricingOriginType: null,
            pricingOriginDriverId: null,
            deliveryQuoteStatus: null,
            deliveryQuoteLockedAtUtc: null,
            deliveryQuoteVersion: 1,
            hasDeliveryAnomalyWarning: false,
            commissionAmount: 5m,
            vatAmount: 15m,
            codFee: 0m);
}
