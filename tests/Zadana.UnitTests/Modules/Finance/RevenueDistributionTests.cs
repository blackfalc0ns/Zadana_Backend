using FluentAssertions;
using Zadana.Application.Modules.Finance.Services;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Finance;

public class RevenueDistributionTests
{
    private static Order BuildOrder(
        decimal subtotal,
        decimal discount,
        decimal deliveryFee,
        decimal vendorCommission,
        decimal driverCommission,
        decimal vat,
        decimal codFee,
        PaymentMethodType method = PaymentMethodType.Card)
    {
        var order = new Order(
            orderNumber: "ORD-TEST-1",
            userId: Guid.NewGuid(),
            vendorId: Guid.NewGuid(),
            customerAddressId: Guid.NewGuid(),
            paymentMethod: method,
            subtotal: subtotal,
            discountTotal: discount,
            deliveryFee: deliveryFee,
            baseDeliveryFee: deliveryFee,
            distanceDeliveryFee: 0,
            surgeDeliveryFee: 0,
            quotedDistanceKm: null,
            deliveryPricingMode: "live",
            deliveryPricingRuleLabel: null,
            driverToVendorDistanceKm: 0,
            vendorToCustomerDistanceKm: 0,
            driverToVendorFee: 0,
            vendorToCustomerFee: 0,
            driverToVendorPricingSource: null,
            vendorToCustomerPricingSource: null,
            usedEstimatedDriverPricing: false,
            pricingOriginType: null,
            pricingOriginDriverId: null,
            deliveryQuoteStatus: null,
            deliveryQuoteLockedAtUtc: null,
            deliveryQuoteVersion: 1,
            hasDeliveryAnomalyWarning: false,
            commissionAmount: vendorCommission,
            vatAmount: vat,
            codFee: codFee);

        order.ApplyFinancialSnapshot(
            productGross: subtotal,
            productNet: subtotal - discount,
            vendorCommissionAmount: vendorCommission,
            driverCommissionAmount: driverCommission,
            currency: "SAR",
            pricingMode: "live",
            taxPolicySnapshot: null,
            commissionPolicySnapshot: null);

        return order;
    }

    [Fact]
    public void Compute_balances_against_total_amount_for_card_order()
    {
        // ProductGross 100, Discount 10, DeliveryFee 15, VAT 15.75, CodFee 0
        var order = BuildOrder(
            subtotal: 100m, discount: 10m, deliveryFee: 15m,
            vendorCommission: 9m, driverCommission: 1.5m,
            vat: 15.75m, codFee: 0m);

        var dist = RevenueDistributionCalculator.Compute(order);

        dist.VendorNet.Should().Be(81m);                  // 90 - 9
        dist.DriverNet.Should().Be(13.5m);                // 15 - 1.5
        dist.PlatformRevenue.Should().Be(10.5m);          // 9 + 1.5 + 0
        dist.TaxPayable.Should().Be(15.75m);
        dist.Total.Should().Be(order.TotalAmount);
    }

    [Fact]
    public void Compute_includes_cod_fee_in_platform_revenue_for_cash_order()
    {
        var order = BuildOrder(
            subtotal: 100m, discount: 0m, deliveryFee: 20m,
            vendorCommission: 8m, driverCommission: 2m,
            vat: 0m, codFee: 5m,
            method: PaymentMethodType.CashOnDelivery);

        var dist = RevenueDistributionCalculator.Compute(order);

        dist.VendorNet.Should().Be(92m);
        dist.DriverNet.Should().Be(18m);
        dist.PlatformRevenue.Should().Be(15m); // 8 + 2 + 5
        dist.TaxPayable.Should().Be(0m);
        dist.Total.Should().Be(order.TotalAmount);
    }

    [Fact]
    public void Compute_subtracts_vendor_recovery_from_vendor_net_and_credits_platform()
    {
        var order = BuildOrder(
            subtotal: 200m, discount: 0m, deliveryFee: 10m,
            vendorCommission: 20m, driverCommission: 1m,
            vat: 0m, codFee: 0m);

        var dist = RevenueDistributionCalculator.Compute(order, vendorRecoveryApplied: 30m);

        dist.VendorNet.Should().Be(150m);            // 200 - 20 - 30
        dist.DriverNet.Should().Be(9m);              // 10 - 1
        dist.PlatformRevenue.Should().Be(51m);       // 20 + 1 + 0 + 30
        dist.TaxPayable.Should().Be(0m);
        dist.Total.Should().Be(order.TotalAmount);
    }

    [Fact]
    public void Compute_throws_when_recovery_is_negative()
    {
        var order = BuildOrder(100m, 0m, 10m, 5m, 0m, 0m, 0m);
        var act = () => RevenueDistributionCalculator.Compute(order, vendorRecoveryApplied: -1m);
        act.Should().Throw<BusinessRuleException>().Which.ErrorCode.Should().Be("INVALID_RECOVERY");
    }
}
