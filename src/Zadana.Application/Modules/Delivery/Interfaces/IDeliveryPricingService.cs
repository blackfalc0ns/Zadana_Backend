namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDeliveryPricingService
{
    Task<DeliveryPriceQuote> QuoteAsync(
        Guid vendorBranchId,
        Guid customerAddressId,
        CancellationToken cancellationToken = default);
}

public sealed record DeliveryPriceQuote(
    decimal BaseFee,
    decimal DistanceFee,
    decimal SurgeFee,
    decimal TotalFee,
    decimal DistanceKm,
    string PricingMode,
    string RuleLabel);
