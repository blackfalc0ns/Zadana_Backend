namespace Zadana.Api.Realtime.Contracts;

public sealed record DeliveryOfferRealtimePayload(
    Guid AssignmentId,
    Guid OrderId,
    string OrderNumber,
    string VendorName,
    decimal DeliveryFee,
    decimal TotalAmount,
    decimal CodAmount,
    string PaymentMethod,
    int CountdownSeconds,
    DateTime Timestamp);
