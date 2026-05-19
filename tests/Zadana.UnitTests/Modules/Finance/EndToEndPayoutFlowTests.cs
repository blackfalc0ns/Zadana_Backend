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
using Zadana.SharedKernel.Finance;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class EndToEndPayoutFlowTests
{
    [Fact]
    public async Task Delivered_order_and_driver_withdrawal_complete_through_payout_gateway()
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

        var vendorPayout = await context.Payouts
            .Include(item => item.Settlement)
            .SingleAsync(item => item.Settlement.OwnerType == SettlementOwnerType.Vendor);
        vendorPayout.Status.Should().Be(PayoutStatus.Paid);
        vendorPayout.ProviderName.Should().Be(FakePaidPayoutGateway.Provider);
        vendorPayout.ProviderTransferId.Should().NotBeNullOrWhiteSpace();
        vendorPayout.Settlement.Status.Should().Be(SettlementStatus.PaidOut);

        var vendorWallet = await context.Wallets.SingleAsync(item =>
            item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == vendor.Id);
        vendorWallet.CurrentBalance.Should().Be(0m);

        var driverUser = new User("Driver User", "driver.e2e@test.com", "0500000002", UserRole.Driver);
        var driver = new Driver(driverUser.Id, null, null, null, region: "RIYADH", city: "Riyadh");
        var driverWallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        driverWallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver User",
            "SA1234567890123456789012",
            "Saudi Bank",
            isPrimary: true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, driverWallet.Id, payoutMethod.Id, 40m);
        var withdrawalHold = new WalletHold(
            WalletOwnerType.Driver,
            driver.Id,
            withdrawal.Amount,
            WalletHoldReason.Withdrawal,
            $"driver-withdrawal:{withdrawal.Id:N}",
            walletId: driverWallet.Id,
            referenceType: "DriverWithdrawalRequest",
            referenceId: withdrawal.Id);

        context.Users.Add(driverUser);
        context.Drivers.Add(driver);
        context.Wallets.Add(driverWallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        context.WalletHolds.Add(withdrawalHold);
        await context.SaveChangesAsync();

        var adminWalletsController = new AdminWalletsController();
        var result = await adminWalletsController.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, null, null),
            context,
            postingService,
            walletProjectionUpdater,
            financeSettings,
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            CancellationToken.None,
            orchestrator);

        result.Should().BeOfType<NoContentResult>();

        var driverWithdrawal = await context.DriverWithdrawalRequests
            .Include(item => item.Payout)
            .SingleAsync(item => item.Id == withdrawal.Id);
        driverWithdrawal.Status.Should().Be(DriverWithdrawalStatus.Paid);
        driverWithdrawal.PayoutId.Should().NotBeNull();
        driverWithdrawal.Payout!.Status.Should().Be(PayoutStatus.Paid);
        driverWithdrawal.Payout.ProviderTransferId.Should().NotBeNullOrWhiteSpace();

        var consumedHold = await context.WalletHolds.SingleAsync(item => item.Id == withdrawalHold.Id);
        consumedHold.Status.Should().Be(WalletHoldStatus.Consumed);

        var refreshedDriverWallet = await context.Wallets.SingleAsync(item => item.Id == driverWallet.Id);
        refreshedDriverWallet.CurrentBalance.Should().Be(60m);

        gateway.CreatedCommands.Should().HaveCount(2);
        gateway.CreatedCommands.Select(item => item.OwnerType).Should().Contain(["Vendor", "Driver"]);
    }

    private static PayoutOrchestrator CreatePayoutOrchestrator(
        IApplicationDbContext context,
        IPayoutGateway gateway)
    {
        var postingService = new FinancialEventPostingService(
            context,
            NullLogger<FinancialEventPostingService>.Instance);

        return new PayoutOrchestrator(
            context,
            [gateway],
            postingService,
            new WalletProjectionUpdater(context),
            Options.Create(new FinancialSettingsOptions()),
            new NoOpAdminAlertService());
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
