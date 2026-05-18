using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

/// <summary>
/// Itemized breakdown of how a refund amount is split across product, delivery,
/// VAT, COD fee, and which party absorbs each piece. The sum of the seven
/// component amounts must equal the refund's approved amount.
/// </summary>
public class RefundAllocation : BaseEntity
{
    public Guid RefundId { get; private set; }

    public decimal ProductAmount { get; private set; }
    public decimal DeliveryAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal CodFeeAmount { get; private set; }

    public decimal PlatformAbsorbedAmount { get; private set; }
    public decimal VendorRecoveryAmount { get; private set; }
    public decimal DriverRecoveryAmount { get; private set; }

    public string CurrencyCode { get; private set; } = CurrencyPolicy.OfficialCurrency;

    public Refund Refund { get; private set; } = null!;

    private RefundAllocation() { }

    public RefundAllocation(
        Guid refundId,
        decimal productAmount,
        decimal deliveryAmount,
        decimal vatAmount,
        decimal codFeeAmount,
        decimal platformAbsorbedAmount,
        decimal vendorRecoveryAmount,
        decimal driverRecoveryAmount,
        string? currencyCode = null)
    {
        if (productAmount < 0 || deliveryAmount < 0 || vatAmount < 0 || codFeeAmount < 0
            || platformAbsorbedAmount < 0 || vendorRecoveryAmount < 0 || driverRecoveryAmount < 0)
        {
            throw new BusinessRuleException(
                "INVALID_REFUND_ALLOCATION",
                "Refund allocation amounts cannot be negative.");
        }

        var normalized = CurrencyPolicy.Normalize(currencyCode);
        CurrencyPolicy.EnsureOfficial(normalized);

        RefundId = refundId;
        ProductAmount = productAmount;
        DeliveryAmount = deliveryAmount;
        VatAmount = vatAmount;
        CodFeeAmount = codFeeAmount;
        PlatformAbsorbedAmount = platformAbsorbedAmount;
        VendorRecoveryAmount = vendorRecoveryAmount;
        DriverRecoveryAmount = driverRecoveryAmount;
        CurrencyCode = normalized;
    }

    /// <summary>
    /// Sum of the line components (product + delivery + VAT + COD fee).
    /// Must match the refund's approved amount.
    /// </summary>
    public decimal LinesTotal => ProductAmount + DeliveryAmount + VatAmount + CodFeeAmount;

    /// <summary>
    /// Sum of cost-bearer slices (platform absorbed + vendor recovery + driver recovery).
    /// Must match <see cref="LinesTotal"/> on a balanced allocation.
    /// </summary>
    public decimal BearerTotal => PlatformAbsorbedAmount + VendorRecoveryAmount + DriverRecoveryAmount;

    public void EnsureBalances(decimal expectedTotal)
    {
        if (LinesTotal != expectedTotal)
        {
            throw new BusinessRuleException(
                "REFUND_ALLOCATION_LINES_MISMATCH",
                $"Refund allocation lines total {LinesTotal} does not match expected {expectedTotal}.");
        }

        if (BearerTotal != expectedTotal)
        {
            throw new BusinessRuleException(
                "REFUND_ALLOCATION_BEARER_MISMATCH",
                $"Refund allocation bearer total {BearerTotal} does not match expected {expectedTotal}.");
        }
    }
}
