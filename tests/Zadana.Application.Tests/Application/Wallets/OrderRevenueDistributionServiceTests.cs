using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Wallets;

public class OrderRevenueDistributionServiceTests
{
    [Fact]
    public async Task DistributeAsync_ShouldDeductOutstandingVendorRecoveryFromFutureRevenue()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.recovery@test.com", "01000000111", UserRole.Customer);
        var vendor = CreateVendor();
        var previousOrderId = Guid.NewGuid();
        var previousCaseId = Guid.NewGuid();
        var outstandingRecovery = new VendorRecovery(vendor.Id, previousOrderId, previousCaseId, 40m);
        var currentOrder = CreateDeliveredPaidOrder(customer.Id, vendor.Id, "ORD-DISTRIBUTE-001");

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(currentOrder);
        dbContext.VendorRecoveries.Add(outstandingRecovery);
        await dbContext.SaveChangesAsync();

        var vendorPayoutWalletService = new VendorPayoutWalletService(
            dbContext,
            new Mock<ILogger<VendorPayoutWalletService>>().Object);
        var vendorRecoveryService = new VendorRecoveryService(dbContext, vendorPayoutWalletService);
        var distributionService = new OrderRevenueDistributionService(
            dbContext,
            Options.Create(new FinancialSettingsOptions()),
            vendorPayoutWalletService,
            new Mock<ILogger<OrderRevenueDistributionService>>().Object,
            vendorRecoveryService);

        await distributionService.DistributeAsync(currentOrder.Id, CancellationToken.None);

        var vendorWallet = await dbContext.Wallets.SingleAsync(wallet =>
            wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendor.Id);
        var platformWallet = await dbContext.Wallets.SingleAsync(wallet =>
            wallet.OwnerType == WalletOwnerType.Platform);
        var recovery = await dbContext.VendorRecoveries.SingleAsync();

        vendorWallet.CurrentBalance.Should().Be(80m);
        platformWallet.CurrentBalance.Should().Be(42.25m);
        recovery.RecoveredAmount.Should().Be(40m);
        recovery.OutstandingAmount.Should().Be(0m);
        recovery.Status.Should().Be(VendorRecoveryStatus.Recovered);
        dbContext.WalletTransactions.Count(txn => txn.OrderId == currentOrder.Id && txn.TxnType == WalletTxnType.OrderRevenue)
            .Should()
            .Be(2);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static Vendor CreateVendor() =>
        new(
            Guid.NewGuid(),
            "متجر استرداد",
            "Recovery Vendor",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.recovery@test.com",
            "01000000999");

    private static Order CreateDeliveredPaidOrder(Guid userId, Guid vendorId, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Recovery Item", 1, 120m));
        order.ChangeStatus(OrderStatus.Delivered);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        return order;
    }
}
