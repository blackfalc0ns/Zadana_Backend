using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Finances.Enums;
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
        var driverId = Guid.NewGuid();

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(currentOrder);
        dbContext.DeliveryAssignments.Add(CreateAssignment(currentOrder.Id, driverId));
        dbContext.VendorRecoveries.Add(outstandingRecovery);
        await dbContext.SaveChangesAsync();

        var distributionService = CreateService(dbContext);

        await distributionService.DistributeAsync(currentOrder.Id, CancellationToken.None);

        var vendorWallet = await dbContext.Wallets.SingleAsync(wallet =>
            wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendor.Id);
        var driverWallet = await dbContext.Wallets.SingleAsync(wallet =>
            wallet.OwnerType == WalletOwnerType.Driver && wallet.OwnerId == driverId);
        var platformWallet = await dbContext.Wallets.SingleAsync(wallet =>
            wallet.OwnerType == WalletOwnerType.Platform);
        var recovery = await dbContext.VendorRecoveries.SingleAsync();

        vendorWallet.CurrentBalance.Should().Be(75m);
        driverWallet.CurrentBalance.Should().Be(12.75m);
        platformWallet.CurrentBalance.Should().Be(47.25m);
        recovery.RecoveredAmount.Should().Be(40m);
        recovery.OutstandingAmount.Should().Be(0m);
        recovery.Status.Should().Be(VendorRecoveryStatus.Recovered);
        dbContext.WalletTransactions.Count(txn => txn.OrderId == currentOrder.Id && txn.TxnType == WalletTxnType.OrderRevenue)
            .Should()
            .Be(3);
    }

    [Fact]
    public async Task DistributeAsync_ShouldClearCustomerAdvanceAndCreditTaxForCardOrder()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.card@test.com", "01000000112", UserRole.Customer);
        var vendor = CreateVendor();
        var order = CreateDeliveredPaidOrder(customer.Id, vendor.Id, "ORD-DISTRIBUTE-002", vatAmount: 15m);
        var driverId = Guid.NewGuid();

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(CreateAssignment(order.Id, driverId));
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).DistributeAsync(order.Id, CancellationToken.None);

        var lines = await dbContext.JournalLines.Where(line => line.OrderId == order.Id).ToListAsync();
        lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.CustomerAdvance &&
            line.DebitAmount == order.TotalAmount &&
            line.OwnerType == FinancialOwnerType.Customer &&
            line.OwnerId == customer.Id);
        lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.TaxPayable &&
            line.CreditAmount == 15m);
        lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.VendorPayable &&
            line.CreditAmount == 115m);
    }

    [Fact]
    public async Task DistributeAsync_ShouldRouteCodFeeToPlatformAndVatToTaxForCodOrder()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.cod@test.com", "01000000113", UserRole.Customer);
        var vendor = CreateVendor();
        var order = CreateDeliveredPaidOrder(
            customer.Id,
            vendor.Id,
            "ORD-DISTRIBUTE-003",
            paymentMethod: PaymentMethodType.CashOnDelivery,
            subtotal: 100m,
            deliveryFee: 20m,
            commissionAmount: 8m,
            vatAmount: 15m,
            codFee: 5m,
            paymentStatus: PaymentStatus.Collected);
        var driverId = Guid.NewGuid();

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(CreateAssignment(order.Id, driverId));
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).DistributeAsync(order.Id, CancellationToken.None);

        var lines = await dbContext.JournalLines.Where(line => line.OrderId == order.Id).ToListAsync();
        lines.Should().ContainSingle(line =>
            line.AccountCode == FinancialAccountCode.DriverCodReceivable &&
            line.DebitAmount == 140m &&
            line.OwnerId == driverId);
        lines.Should().ContainSingle(line => line.AccountCode == FinancialAccountCode.VendorPayable && line.CreditAmount == 92m);
        lines.Should().ContainSingle(line => line.AccountCode == FinancialAccountCode.DriverPayable && line.CreditAmount == 17m);
        lines.Should().ContainSingle(line => line.AccountCode == FinancialAccountCode.PlatformRevenue && line.CreditAmount == 16m);
        lines.Should().ContainSingle(line => line.AccountCode == FinancialAccountCode.TaxPayable && line.CreditAmount == 15m);
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
            "متجر استرجاع",
            "Recovery Vendor",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.recovery@test.com",
            "01000000999");

    private static OrderRevenueDistributionService CreateService(ApplicationDbContext dbContext)
    {
        var vendorPayoutWalletService = new VendorPayoutWalletService(
            dbContext,
            new Mock<ILogger<VendorPayoutWalletService>>().Object);
        var vendorRecoveryService = new VendorRecoveryService(dbContext, vendorPayoutWalletService);
        var financialEventPostingService = new FinancialEventPostingService(
            dbContext,
            new Mock<ILogger<FinancialEventPostingService>>().Object);
        var walletProjectionUpdater = new WalletProjectionUpdater(dbContext);
        return new OrderRevenueDistributionService(
            dbContext,
            Options.Create(new FinancialSettingsOptions()),
            vendorPayoutWalletService,
            financialEventPostingService,
            walletProjectionUpdater,
            new Mock<ILogger<OrderRevenueDistributionService>>().Object,
            vendorRecoveryService);
    }

    private static DeliveryAssignment CreateAssignment(Guid orderId, Guid driverId)
    {
        var assignment = new DeliveryAssignment(orderId, 0m);
        assignment.OfferTo(driverId, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        return assignment;
    }

    private static Order CreateDeliveredPaidOrder(
        Guid userId,
        Guid vendorId,
        string orderNumber,
        PaymentMethodType paymentMethod = PaymentMethodType.Card,
        decimal subtotal = 120m,
        decimal deliveryFee = 15m,
        decimal commissionAmount = 5m,
        decimal vatAmount = 0m,
        decimal codFee = 0m,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), paymentMethod, subtotal, 0m, deliveryFee, deliveryFee, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, null, null, false, null, null, null, null, 1, false, commissionAmount, vatAmount, codFee);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Recovery Item", 1, subtotal));
        order.ChangeStatus(OrderStatus.Delivered);
        order.UpdatePaymentStatus(paymentStatus);
        return order;
    }
}
