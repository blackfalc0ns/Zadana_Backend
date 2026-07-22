using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Api.Modules.Wallets.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Serialization;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class EndToEndPayoutFlowTests
{
    [Fact]
    public async Task Delivered_order_does_not_trigger_legacy_per_order_payout()
    {
        await using var context = TestDbContextFactory.Create();
        var gateway = new FakePaidPayoutGateway();
        var orchestrator = CreatePayoutOrchestrator(context, gateway);

        var vendor = CreatePerOrderVendor();
        var vendorBank = CreateVerifiedPrimaryBankAccount(vendor.Id, "Vendor Owner");
        var customer = new User("Customer User", "customer.e2e@test.com", "0500000001", UserRole.Customer);
        var order = CreateDeliveredPaidOrder(customer.Id, vendor.Id, "ORD-PAYOUT-E2E-001");

        context.Users.Add(customer);
        context.Vendors.Add(vendor);
        context.VendorBankAccounts.Add(vendorBank);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var financeSettings = Options.Create(new FinancialSettingsOptions());
        var postingService = new FinancialEventPostingService(
            context,
            NullLogger<FinancialEventPostingService>.Instance);
        var walletProjectionUpdater = new WalletProjectionUpdater(context);
        var vendorPayoutWalletService = new VendorPayoutWalletService(
            context,
            NullLogger<VendorPayoutWalletService>.Instance);
        var distributionService = new OrderRevenueDistributionService(
            context,
            financeSettings,
            vendorPayoutWalletService,
            postingService,
            walletProjectionUpdater,
            NullLogger<OrderRevenueDistributionService>.Instance,
            payoutOrchestrator: orchestrator);

        await distributionService.DistributeAsync(order.Id, CancellationToken.None);

        var vendorPayouts = await context.Payouts
            .Include(item => item.Settlement)
            .Where(item => item.Settlement.OwnerType == SettlementOwnerType.Vendor)
            .ToListAsync();
        vendorPayouts.Should().BeEmpty();

        var vendorWallet = await context.Wallets.SingleAsync(item =>
            item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == vendor.Id);
        vendorWallet.CurrentBalance.Should().BeGreaterThan(0m);
        vendor.FinancialLifecycleMode.Should().Be(VendorFinancialLifecycleMode.Weekly);
        gateway.CreatedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task Scheduled_vendor_payout_requires_admin_approval_before_trigger()
    {
        await using var context = TestDbContextFactory.Create();
        var gateway = new FakePaidPayoutGateway();
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(item => item.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        settlementSettings
            .Setup(item => item.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enum.GetValues<PayoutScheduleDay>());
        var orchestrator = CreatePayoutOrchestrator(context, gateway, settlementSettings.Object);

        var vendor = CreatePerOrderVendor();
        vendor.UpdateFinanceSettings(VendorFinancialLifecycleMode.Weekly);
        vendor.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        var vendorBank = CreateVerifiedPrimaryBankAccount(vendor.Id, "Vendor Owner");
        var settlement = new Settlement(vendor.Id, null, SettlementOrigin.ScheduledCycle);
        settlement.UpdateTotals(100m, 0m);
        var payout = new Payout(settlement.Id, settlement.NetAmount, vendorBank.Id);
        payout.SetScheduledPayoutDay(vendor.PayoutDay);
        payout.PrepareDestination(
            PayoutDestinationType.VendorBankAccount,
            PayoutDestinationSnapshotCodec.CreateVendorBankAccount(vendorBank));

        context.Vendors.Add(vendor);
        context.VendorBankAccounts.Add(vendorBank);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        await orchestrator.Awaiting(item => item.TriggerAsync(payout.Id))
            .Should()
            .ThrowAsync<BusinessRuleException>()
            .WithMessage("*approved*");

        gateway.CreatedCommands.Should().BeEmpty();

        settlement.Approve();
        await context.SaveChangesAsync();

        await orchestrator.TriggerAsync(payout.Id);

        var refreshedPayout = await context.Payouts.SingleAsync(item => item.Id == payout.Id);
        refreshedPayout.Status.Should().Be(PayoutStatus.Paid);
        gateway.CreatedCommands.Should().ContainSingle();
    }

    private static PayoutOrchestrator CreatePayoutOrchestrator(
        IApplicationDbContext context,
        IPayoutGateway gateway,
        ISettlementProcessingSettingsService? settlementProcessingSettingsService = null)
    {
        var postingService = new FinancialEventPostingService(
            context,
            NullLogger<FinancialEventPostingService>.Instance);
        var vendorPayoutWalletService = new VendorPayoutWalletService(
            context,
            NullLogger<VendorPayoutWalletService>.Instance);

        return new PayoutOrchestrator(
            context,
            [gateway],
            postingService,
            new WalletProjectionUpdater(context),
            vendorPayoutWalletService,
            Options.Create(new FinancialSettingsOptions()),
            new NoOpAdminAlertService(),
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            settlementProcessingSettingsService);
    }

    private static Vendor CreatePerOrderVendor()
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "تاجر اختبار",
            "E2E Vendor",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.e2e@test.com",
            "0500000000",
            region: "Riyadh",
            city: "Riyadh");
        vendor.UpdateFinanceSettings(VendorFinancialLifecycleMode.PerOrderDirectPayout);
        return vendor;
    }

    private static VendorBankAccount CreateVerifiedPrimaryBankAccount(Guid vendorId, string holderName)
    {
        var bankAccount = new VendorBankAccount(
            vendorId,
            "Saudi Bank",
            holderName,
            "SA1234567890123456789012");
        bankAccount.Verify(Guid.NewGuid());
        bankAccount.SetAsPrimary();
        return bankAccount;
    }

    private static Order CreateDeliveredPaidOrder(Guid userId, Guid vendorId, string orderNumber)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            Guid.NewGuid(),
            PaymentMethodType.Card,
            120m,
            0m,
            15m,
            15m,
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
            5m);

        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "E2E Item", 1, 120m));
        order.ChangeStatus(OrderStatus.Delivered);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        return order;
    }

    private sealed class FakePaidPayoutGateway : IPayoutGateway
    {
        public const string Provider = "FakeMoyasar";
        public List<CreatePayoutCommand> CreatedCommands { get; } = [];
        public string ProviderName => Provider;
        public bool IsEnabled => true;

        public Task<PayoutGatewayResult> CreatePayoutAsync(
            CreatePayoutCommand command,
            CancellationToken cancellationToken)
        {
            CreatedCommands.Add(command);
            return Task.FromResult(new PayoutGatewayResult(
                Provider,
                $"pout_{command.PayoutId:N}"[..20],
                "paid",
                ProviderSequenceNumber: command.SequenceNumber));
        }

        public Task<PayoutGatewayDetails> FetchPayoutAsync(
            string providerTransferId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PayoutGatewayDetails(
                Provider,
                providerTransferId,
                "paid",
                0m,
                CurrencyPolicy.OfficialCurrency));
    }

    private sealed class NoOpAdminAlertService : IAdminAlertService
    {
        public Task<AdminAlertDispatchResult> SendAsync(
            AdminAlertRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminAlertDispatchResult(
                0,
                0,
                new OneSignalPushDispatchResult(false, false, true, null, null, "test")));
    }
}
