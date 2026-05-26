using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class RevenueReconciliationServiceTests
{
    [Fact]
    public async Task PreviewAsync_ShouldDetectLegacyOnlineDistributionDeltas()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateDeliveredOrder();
        var driverId = Guid.NewGuid();
        context.Orders.Add(order);
        context.DeliveryAssignments.Add(CreateAssignment(order.Id, driverId));
        await context.SaveChangesAsync();

        var postingService = CreatePostingService(context);
        await postingService.PostAsync(
            FinancialEventType.OnlinePaymentDelivered,
            $"legacy-delivery:{order.Id:N}",
            [
                new JournalLineDraft(FinancialAccountCode.GatewayReceivable, 125m, 0m, FinancialOwnerType.Gateway, OrderId: order.Id),
                new JournalLineDraft(FinancialAccountCode.VendorPayable, 0m, 110m, FinancialOwnerType.Vendor, order.VendorId, order.Id),
                new JournalLineDraft(FinancialAccountCode.DriverPayable, 0m, 8.5m, FinancialOwnerType.Driver, driverId, order.Id),
                new JournalLineDraft(FinancialAccountCode.PlatformRevenue, 0m, 6.5m, FinancialOwnerType.Platform, Guid.Parse("00000000-0000-0000-0000-000000000001"), order.Id)
            ],
            orderId: order.Id);

        var service = CreateService(context);
        var preview = await service.PreviewAsync();

        preview.OrdersChecked.Should().Be(1);
        preview.AffectedOrders.Should().Be(1);
        var delta = preview.Orders.Single();
        delta.AccountDeltas[FinancialAccountCode.CustomerAdvance.ToString()].Should().Be(125m);
        delta.AccountDeltas[FinancialAccountCode.GatewayReceivable.ToString()].Should().Be(-125m);
        delta.AccountDeltas[FinancialAccountCode.VendorPayable.ToString()].Should().Be(15m);
        delta.AccountDeltas[FinancialAccountCode.TaxPayable.ToString()].Should().Be(-15m);
    }

    [Fact]
    public async Task ApplyAsync_ShouldPostIdempotentRevenueAdjustment()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateDeliveredOrder();
        var driverId = Guid.NewGuid();
        context.Orders.Add(order);
        context.DeliveryAssignments.Add(CreateAssignment(order.Id, driverId));
        await context.SaveChangesAsync();

        var postingService = CreatePostingService(context);
        var projectionUpdater = new WalletProjectionUpdater(context);
        await postingService.PostAsync(
            FinancialEventType.OnlinePaymentDelivered,
            $"legacy-delivery:{order.Id:N}",
            [
                new JournalLineDraft(FinancialAccountCode.GatewayReceivable, 125m, 0m, FinancialOwnerType.Gateway, OrderId: order.Id),
                new JournalLineDraft(FinancialAccountCode.VendorPayable, 0m, 110m, FinancialOwnerType.Vendor, order.VendorId, order.Id),
                new JournalLineDraft(FinancialAccountCode.DriverPayable, 0m, 8.5m, FinancialOwnerType.Driver, driverId, order.Id),
                new JournalLineDraft(FinancialAccountCode.PlatformRevenue, 0m, 6.5m, FinancialOwnerType.Platform, Guid.Parse("00000000-0000-0000-0000-000000000001"), order.Id)
            ],
            orderId: order.Id);

        var service = CreateService(context, postingService, projectionUpdater);
        var firstApply = await service.ApplyAsync();
        var secondApply = await service.ApplyAsync();

        firstApply.AdjustmentsPosted.Should().Be(1);
        secondApply.AdjustmentsPosted.Should().Be(0);
        context.FinancialEvents.Count(item => item.EventType == FinancialEventType.FinancialAdjustmentApplied).Should().Be(1);
    }

    [Fact]
    public async Task PreviewAsync_WhenOrderDistributionCannotBeCalculated_ShouldSkipOrder()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateDeliveredOrder();
        order.ApplyFinancialSnapshot(
            productGross: 100m,
            productNet: 1m,
            vendorCommissionAmount: 0m,
            driverCommissionAmount: 0m,
            currency: "SAR",
            pricingMode: "live",
            taxPolicySnapshot: null,
            commissionPolicySnapshot: "{}");
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var preview = await service.PreviewAsync();

        preview.OrdersChecked.Should().Be(1);
        preview.AffectedOrders.Should().Be(0);
        preview.Orders.Should().ContainSingle();
        preview.Orders[0].CanApply.Should().BeFalse();
        preview.Orders[0].SkipReason.Should().Contain("Unable to calculate expected distribution");
    }

    [Fact]
    public async Task PreviewAsync_WhenNonCashOrderHasNoFundingSource_ShouldSkipAdjustment()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateDeliveredOrder();
        context.Orders.Add(order);
        context.DeliveryAssignments.Add(CreateAssignment(order.Id, Guid.NewGuid()));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var preview = await service.PreviewAsync();

        preview.OrdersChecked.Should().Be(1);
        preview.AffectedOrders.Should().Be(0);
        preview.Orders.Should().ContainSingle();
        preview.Orders[0].CanApply.Should().BeFalse();
        preview.Orders[0].SkipReason.Should().Contain("no CustomerAdvance funding");
    }

    [Fact]
    public async Task PreviewAsync_WhenWalletOrderHasNoFundingSource_ShouldSkipWithWalletFundingReason()
    {
        await using var context = TestDbContextFactory.Create();
        var order = CreateDeliveredOrder(PaymentMethodType.Wallet);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var preview = await service.PreviewAsync();

        preview.OrdersChecked.Should().Be(1);
        preview.AffectedOrders.Should().Be(0);
        preview.Orders.Should().ContainSingle();
        preview.Orders[0].CanApply.Should().BeFalse();
        preview.Orders[0].SkipReason.Should().Contain("Standalone wallet checkout is not supported yet");
    }

    private static RevenueReconciliationService CreateService(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        FinancialEventPostingService? postingService = null,
        WalletProjectionUpdater? projectionUpdater = null)
    {
        return new RevenueReconciliationService(
            context,
            Options.Create(new FinancialSettingsOptions()),
            postingService ?? CreatePostingService(context),
            projectionUpdater ?? new WalletProjectionUpdater(context));
    }

    private static FinancialEventPostingService CreatePostingService(Zadana.Infrastructure.Persistence.ApplicationDbContext context) =>
        new(context, Mock.Of<ILogger<FinancialEventPostingService>>());

    private static DeliveryAssignment CreateAssignment(Guid orderId, Guid driverId)
    {
        var assignment = new DeliveryAssignment(orderId, 0m);
        assignment.OfferTo(driverId, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        return assignment;
    }

    private static Order CreateDeliveredOrder(PaymentMethodType paymentMethod = PaymentMethodType.Card)
    {
        var order = new Order(
            "ORD-RECON-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            paymentMethod,
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

        order.ChangeStatus(OrderStatus.Delivered);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        return order;
    }
}
