using System.Globalization;

namespace Zadana.Application.Modules.Payments.DTOs;

public sealed record PaymobCheckoutResponseDto
{
    public PaymobCheckoutResponseDto(string message, PaymobCheckoutOrderDto order, PaymobCheckoutPaymentDto payment)
    {
        Message = message;
        Order = order;
        Payment = payment;
    }

    public PaymobCheckoutResponseDto(string messageAr, string messageEn, PaymobCheckoutOrderDto order, PaymobCheckoutPaymentDto payment)
        : this(PaymentDtoLocalization.Localize(messageAr, messageEn), order, payment)
    {
    }

    public string Message { get; init; }
    public PaymobCheckoutOrderDto Order { get; init; }
    public PaymobCheckoutPaymentDto Payment { get; init; }
}

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

public sealed record PaymobWebhookProcessResultDto
{
    public PaymobWebhookProcessResultDto(string message, Guid paymentId, string status)
    {
        Message = message;
        PaymentId = paymentId;
        Status = status;
    }

    public PaymobWebhookProcessResultDto(string messageAr, string messageEn, Guid paymentId, string status)
        : this(PaymentDtoLocalization.Localize(messageAr, messageEn), paymentId, status)
    {
    }

    public string Message { get; init; }
    public Guid PaymentId { get; init; }
    public string Status { get; init; }
}

public sealed record PaymobPaymentConfirmationResultDto
{
    public PaymobPaymentConfirmationResultDto(
        string message,
        Guid paymentId,
        string paymentStatus,
        Guid userId,
        Guid orderId,
        string orderStatus,
        bool alreadyConfirmed)
    {
        Message = message;
        PaymentId = paymentId;
        PaymentStatus = paymentStatus;
        UserId = userId;
        OrderId = orderId;
        OrderStatus = orderStatus;
        AlreadyConfirmed = alreadyConfirmed;
    }

    public PaymobPaymentConfirmationResultDto(
        string messageAr,
        string messageEn,
        Guid paymentId,
        string paymentStatus,
        Guid userId,
        Guid orderId,
        string orderStatus,
        bool alreadyConfirmed)
        : this(PaymentDtoLocalization.Localize(messageAr, messageEn), paymentId, paymentStatus, userId, orderId, orderStatus, alreadyConfirmed)
    {
    }

    public string Message { get; init; }
    public Guid PaymentId { get; init; }
    public string PaymentStatus { get; init; }
    public Guid UserId { get; init; }
    public Guid OrderId { get; init; }
    public string OrderStatus { get; init; }
    public bool AlreadyConfirmed { get; init; }
}

internal static class PaymentDtoLocalization
{
    public static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}
