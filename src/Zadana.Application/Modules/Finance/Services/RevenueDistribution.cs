using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Finance.Services;

/// <summary>
/// Pure value object describing the post-delivery revenue split for a single order.
/// All amounts are in the order's currency (SAR per the revised workflow).
/// </summary>
public sealed record RevenueDistribution(
    decimal VendorNet,
    decimal DriverNet,
    decimal PlatformRevenue,
    decimal TaxPayable,
    decimal VendorRecoveryApplied)
{
    public decimal Total => VendorNet + DriverNet + PlatformRevenue + TaxPayable;
}

/// <summary>
/// Computes the revenue split per the formula in section 9 of the revised spec:
/// <code>
/// VendorNet      = ProductNet - VendorCommissionAmount - VendorRecoveryApplied
/// DriverNet      = DeliveryFee - DriverCommissionAmount
/// PlatformRevenue = VendorCommissionAmount + DriverCommissionAmount + CodFee + VendorRecoveryApplied
/// TaxPayable     = VatAmount
/// </code>
/// The result must satisfy <c>Total == Order.TotalAmount</c>.
/// </summary>
public static class RevenueDistributionCalculator
{
    public static RevenueDistribution Compute(Order order, decimal vendorRecoveryApplied = 0m)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (vendorRecoveryApplied < 0)
        {
            throw new BusinessRuleException(
                "INVALID_RECOVERY",
                "Vendor recovery applied to revenue distribution cannot be negative.");
        }

        // Currency guard. The order may still be EGP for legacy data; only enforce
        // SAR when an explicit currency snapshot exists on the order.
        if (!string.IsNullOrWhiteSpace(order.Currency))
        {
            CurrencyPolicy.EnsureOfficial(order.Currency);
        }

        var productNet = order.ProductNet > 0 ? order.ProductNet : Math.Max(0, order.Subtotal - order.DiscountTotal);
        var vendorComm = order.VendorCommissionAmount > 0 ? order.VendorCommissionAmount : order.CommissionAmount;
        var driverComm = order.DriverCommissionAmount;
        var deliveryFee = order.DeliveryFee;
        var vat = order.VatAmount;
        var cod = order.CodFee;

        var vendorNet = productNet - vendorComm - vendorRecoveryApplied;
        var driverNet = deliveryFee - driverComm;
        var platformRevenue = vendorComm + driverComm + cod + vendorRecoveryApplied;
        var tax = vat;

        var distribution = new RevenueDistribution(vendorNet, driverNet, platformRevenue, tax, vendorRecoveryApplied);

        var sum = distribution.Total;
        if (sum != order.TotalAmount)
        {
            throw new BusinessRuleException(
                "REVENUE_DISTRIBUTION_IMBALANCE",
                $"Revenue distribution does not balance. Sum = {sum}, Order.TotalAmount = {order.TotalAmount}.");
        }

        if (vendorNet < 0 || driverNet < 0 || platformRevenue < 0 || tax < 0)
        {
            throw new BusinessRuleException(
                "REVENUE_DISTRIBUTION_NEGATIVE",
                "Revenue distribution produced a negative leg. Inputs are inconsistent.");
        }

        return distribution;
    }
}
