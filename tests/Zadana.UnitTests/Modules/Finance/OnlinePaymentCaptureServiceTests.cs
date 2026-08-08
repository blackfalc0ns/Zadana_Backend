using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class OnlinePaymentCaptureServiceTests
{
    [Fact]
    public async Task PostCapturedAsync_creates_balanced_journal_entry()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment) = SeedOrder(context, totalAmount: 130.75m, currency: "SAR");
        await context.SaveChangesAsync();

        var service = BuildService(context);
        await service.PostCapturedAsync(order, payment, "Moyasar", "moyasar-pay-1", CancellationToken.None);

        var entry = context.JournalEntries.Include(x => x.Lines).Single();
        entry.Lines.Sum(l => l.DebitAmount).Should().Be(entry.Lines.Sum(l => l.CreditAmount));
        entry.Lines.Should().HaveCount(4);
        entry.Lines.Should().ContainSingle(l => l.AccountCode == FinancialAccountCode.GatewayReceivable && l.DebitAmount == 130.75m);
        entry.Lines.Should().ContainSingle(l => l.AccountCode == FinancialAccountCode.CustomerAdvance && l.CreditAmount == 130.75m);
        entry.Lines.Should().ContainSingle(l => l.AccountCode == FinancialAccountCode.GatewayFeeExpense && l.DebitAmount == 4.60m);
        entry.Lines.Should().ContainSingle(l => l.AccountCode == FinancialAccountCode.GatewayReceivable && l.CreditAmount == 4.60m);
    }

    [Fact]
    public async Task PostCapturedAsync_is_idempotent_per_provider_payment_id()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment) = SeedOrder(context, totalAmount: 50m, currency: "SAR");
        await context.SaveChangesAsync();

        var service = BuildService(context);
        await service.PostCapturedAsync(order, payment, "Moyasar", "moyasar-pay-2", CancellationToken.None);
        await service.PostCapturedAsync(order, payment, "Moyasar", "moyasar-pay-2", CancellationToken.None);

        context.FinancialEvents.Count().Should().Be(1);
        context.JournalEntries.Count().Should().Be(1);
    }

    [Fact]
    public async Task PostCapturedAsync_skips_non_sar_orders()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment) = SeedOrder(context, totalAmount: 50m, currency: "EGP");
        await context.SaveChangesAsync();

        var service = BuildService(context);
        await service.PostCapturedAsync(order, payment, "Moyasar", "moyasar-pay-3", CancellationToken.None);

        context.FinancialEvents.Should().BeEmpty();
    }

    [Fact]
    public Task PostCapturedAsync_uses_idempotency_key_with_provider_and_id()
    {
        var key = OnlinePaymentCaptureService.BuildIdempotencyKey("Moyasar", "abc-123");
        key.Should().Be("payment-captured:moyasar:abc-123");
        return Task.CompletedTask;
    }

    private static OnlinePaymentCaptureService BuildService(Zadana.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var posting = new FinancialEventPostingService(context, NullLogger<FinancialEventPostingService>.Instance);
        var projection = new WalletProjectionUpdater(context);
        return new OnlinePaymentCaptureService(context, posting, projection, NullLogger<OnlinePaymentCaptureService>.Instance);
    }

    private static (Order Order, Payment Payment) SeedOrder(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        decimal totalAmount,
        string currency)
    {
        var order = new Order(
            orderNumber: "ORD-CAP-1",
            userId: Guid.NewGuid(),
            vendorId: Guid.NewGuid(),
            customerAddressId: Guid.NewGuid(),
            paymentMethod: PaymentMethodType.Card,
            subtotal: totalAmount,
            discountTotal: 0m,
            deliveryFee: 0m,
            baseDeliveryFee: 0m,
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
            commissionAmount: 0m);

        order.ApplyFinancialSnapshot(
            productGross: totalAmount,
            productNet: totalAmount,
            vendorCommissionAmount: 0m,
            driverCommissionAmount: 0m,
            currency: currency,
            pricingMode: "live",
            taxPolicySnapshot: null,
            commissionPolicySnapshot: null);

        context.Orders.Add(order);

        var payment = new Payment(order.Id, PaymentMethodType.Card, totalAmount);
        context.Payments.Add(payment);
        return (order, payment);
    }
}
