using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Modules.Vendors.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Controllers;

public class VendorWorkspaceFinanceControllerTests
{
    [Fact]
    public async Task GetFinance_Today_UsesDeliveredDateAndFiltersLedgerToPeriod()
    {
        await using var context = TestDbContextFactory.Create();

        var vendorId = Guid.NewGuid();
        await SeedVendorFinanceDataAsync(context, vendorId);

        var controller = new VendorWorkspaceController(context, new StubCurrentVendorService(vendorId));

        var result = await controller.GetFinance("today");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var snapshot = okResult.Value.Should().BeOfType<VendorFinanceSnapshotResponse>().Subject;

        snapshot.Kpis.Single(kpi => kpi.Id == "gross-sales").Value.Should().Be(110m);
        snapshot.Kpis.Single(kpi => kpi.Id == "vendor-profit").Value.Should().Be(30m);
        snapshot.Kpis.Single(kpi => kpi.Id == "platform-fees").Value.Should().Be(15m);
        snapshot.Kpis.Single(kpi => kpi.Id == "vendor-net").Value.Should().Be(85m);
        snapshot.HoldAmount.Should().Be(80m);
        snapshot.AvailableBalance.Should().Be(370m);
        snapshot.Ledger.Should().ContainSingle(entry => entry.Reference == "TODAY-TXN");
        snapshot.Ledger.Should().NotContain(entry => entry.Reference == "OLD-TXN");
        snapshot.Trend.Should().HaveCount(8);
    }

    [Fact]
    public async Task GetFinance_Quarter_ReturnsMonthlyTrendBuckets()
    {
        await using var context = TestDbContextFactory.Create();

        var vendorId = Guid.NewGuid();
        await SeedVendorFinanceDataAsync(context, vendorId);

        var controller = new VendorWorkspaceController(context, new StubCurrentVendorService(vendorId));

        var result = await controller.GetFinance("quarter");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var snapshot = okResult.Value.Should().BeOfType<VendorFinanceSnapshotResponse>().Subject;

        snapshot.Trend.Should().HaveCount(3);
        snapshot.Trend.Select(point => point.Label).Should().OnlyContain(label => label.Length == 3);
    }

    private static async Task SeedVendorFinanceDataAsync(IApplicationDbContext context, Guid vendorId)
    {
        var user = new User("Finance User", "finance@test.com", "0500000000", UserRole.Customer);
        var vendorOwner = new User("Vendor Owner", "vendor@test.com", "0511111111", UserRole.Vendor);
        var address = new CustomerAddress(user.Id, "Finance User", "0500000000", "Test Address", AddressLabel.Home, city: "Cairo");
        var vendor = new Vendor(
            vendorOwner.Id,
            "متجر مالي",
            "Finance Store",
            "Retail",
            "CR-123",
            "store@test.com",
            "0522222222",
            payoutCycle: "weekly");

        SetPrivateProperty(vendor, nameof(Vendor.Id), vendorId);

        var bankAccount = new VendorBankAccount(vendorId, "Bank Test", "Vendor Owner", "SA1234567890123456789012");
        bankAccount.MarkAsPreferredForSetup();

        var deliveredToday = CreateDeliveredOrder(
            user.Id,
            vendorId,
            address.Id,
            "ORD-TODAY",
            subtotal: 100m,
            deliveryFee: 10m,
            commissionAmount: 15m,
            deliveredAtUtc: DateTime.UtcNow.Date.AddHours(10));

        var deliveredLastWeek = CreateDeliveredOrder(
            user.Id,
            vendorId,
            address.Id,
            "ORD-OLD",
            subtotal: 200m,
            deliveryFee: 10m,
            commissionAmount: 20m,
            deliveredAtUtc: DateTime.UtcNow.Date.AddDays(-8).AddHours(12));

        var todayItem = new OrderItem(deliveredToday.Id, Guid.NewGuid(), Guid.NewGuid(), "Oil", 2, 50m, vendorProfitPerUnit: 15m);
        var oldItem = new OrderItem(deliveredLastWeek.Id, Guid.NewGuid(), Guid.NewGuid(), "Filter", 2, 100m, vendorProfitPerUnit: 25m);

        var wallet = new Wallet(WalletOwnerType.Vendor, vendorId);
        wallet.Credit(500m);
        wallet.Hold(50m);
        var activeHold = new WalletHold(
            WalletOwnerType.Vendor,
            vendorId,
            30m,
            WalletHoldReason.Payout,
            $"vendor-finance-test:{vendorId:N}");

        var todayTransaction = new WalletTransaction(wallet.Id, WalletTxnType.Payout, 80m, "OUT", referenceType: "TODAY-TXN", description: "Today payout");
        SetPrivateProperty(todayTransaction, nameof(WalletTransaction.CreatedAtUtc), DateTime.UtcNow.Date.AddHours(14));

        var oldTransaction = new WalletTransaction(wallet.Id, WalletTxnType.Payout, 40m, "OUT", referenceType: "OLD-TXN", description: "Old payout");
        SetPrivateProperty(oldTransaction, nameof(WalletTransaction.CreatedAtUtc), DateTime.UtcNow.Date.AddDays(-8).AddHours(9));

        var settlement = new Settlement(vendorId, null);
        settlement.UpdateTotals(200m, 20m);
        settlement.MarkAsProcessing();

        var payout = new Payout(settlement.Id, 80m, bankAccount.Id);
        payout.MarkAsPaid("TXN-123");
        SetPrivateProperty(payout, nameof(Payout.ProcessedAtUtc), DateTime.UtcNow.Date.AddHours(15));

        context.Users.AddRange(user, vendorOwner);
        context.CustomerAddresses.Add(address);
        context.Vendors.Add(vendor);
        context.VendorBankAccounts.Add(bankAccount);
        context.Orders.AddRange(deliveredToday, deliveredLastWeek);
        context.OrderItems.AddRange(todayItem, oldItem);
        context.Wallets.Add(wallet);
        context.WalletHolds.Add(activeHold);
        context.WalletTransactions.AddRange(todayTransaction, oldTransaction);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();
    }

    private static Order CreateDeliveredOrder(
        Guid userId,
        Guid vendorId,
        Guid addressId,
        string orderNumber,
        decimal subtotal,
        decimal deliveryFee,
        decimal commissionAmount,
        DateTime deliveredAtUtc)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            addressId,
            PaymentMethodType.CashOnDelivery,
            subtotal,
            0m,
            deliveryFee,
            deliveryFee,
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
            commissionAmount);

        SetPrivateProperty(order, nameof(Order.Status), OrderStatus.Delivered);
        SetPrivateProperty(order, nameof(Order.PaymentStatus), PaymentStatus.Paid);
        SetPrivateProperty(order, nameof(Order.DeliveredAtUtc), deliveredAtUtc);
        SetPrivateProperty(order, nameof(Order.PlacedAtUtc), deliveredAtUtc.AddHours(-2));

        return order;
    }

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull($"property {propertyName} should exist on {target.GetType().Name}");
        property!.SetValue(target, value);
    }

    private sealed class StubCurrentVendorService(Guid vendorId) : ICurrentVendorService
    {
        public Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(vendorId);
        public Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(vendorId);
    }
}
