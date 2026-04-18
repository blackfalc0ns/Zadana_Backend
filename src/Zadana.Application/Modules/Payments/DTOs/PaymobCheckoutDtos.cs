namespace Zadana.Application.Modules.Payments.DTOs;

public record PaymobCheckoutResponseDto(
    string Message,
    PaymobCheckoutOrderDto Order,
    PaymobCheckoutPaymentDto Payment);

public record PaymobCheckoutOrderDto(
    Guid Id,
    string Status,
    decimal Total,
    string PaymentMethodId);

public record PaymobCheckoutPaymentDto(
    Guid Id,
    string Provider,
    string Status,
    string IframeUrl,
    string ProviderReference);

public record PaymobCheckoutSessionRequest(
    Guid PaymentId,
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    IReadOnlyCollection<PaymobOrderItemRequest> Items,
    string CustomerFirstName,
    string CustomerLastName,
    string CustomerEmail,
    string CustomerPhone,
    string AddressLine,
    string City,
    string CountryCode);

public record PaymobOrderItemRequest(
    string Name,
    string Description,
    int Quantity,
    decimal UnitPrice);

public record PaymobCheckoutSessionDto(
    string ProviderReference,
    string PaymentToken,
    string IframeUrl);

public record PaymobWebhookNotificationDto(
    Guid? PaymentId,
    string? ProviderReference,
    string? ProviderTransactionId,
    bool IsSuccess,
    bool IsPending,
    string EventType);

public record PaymobWebhookProcessResultDto(
    string Message,
    Guid PaymentId,
    string Status);

public record PaymobPaymentConfirmationResultDto(
    string Message,
    Guid PaymentId,
    string PaymentStatus,
    Guid UserId,
    Guid OrderId,
    string OrderStatus,
    bool AlreadyConfirmed);
