using System.Globalization;

namespace Zadana.Application.Modules.Payments.DTOs;

/// <summary>
/// Provider-agnostic checkout response returned to the customer app when a
/// Moyasar (or future provider) card session is successfully created.
/// </summary>
public sealed record CardCheckoutResponseDto
{
    public CardCheckoutResponseDto(string message, CardCheckoutOrderDto order, CardCheckoutPaymentDto payment)
    {
        Message = message;
        Order = order;
        Payment = payment;
    }

    public CardCheckoutResponseDto(string messageAr, string messageEn, CardCheckoutOrderDto order, CardCheckoutPaymentDto payment)
        : this(CardPaymentDtoLocalization.Localize(messageAr, messageEn), order, payment)
    {
    }

    public string Message { get; init; }
    public CardCheckoutOrderDto Order { get; init; }
    public CardCheckoutPaymentDto Payment { get; init; }
}

public record CardCheckoutOrderDto(
    Guid Id,
    string Status,
    decimal Total,
    string PaymentMethodId);

public record CardCheckoutPaymentDto(
    Guid Id,
    string Provider,
    string Status,
    string ClientAction,
    object? ProviderConfig,
    string? ProviderReference);

public sealed record CardPaymentConfirmationResultDto
{
    public CardPaymentConfirmationResultDto(
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

    public CardPaymentConfirmationResultDto(
        string messageAr,
        string messageEn,
        Guid paymentId,
        string paymentStatus,
        Guid userId,
        Guid orderId,
        string orderStatus,
        bool alreadyConfirmed)
        : this(CardPaymentDtoLocalization.Localize(messageAr, messageEn), paymentId, paymentStatus, userId, orderId, orderStatus, alreadyConfirmed)
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

public sealed record PaymentWebhookProcessResultDto(
    string Message,
    Guid PaymentId,
    string Status,
    string? ProviderPaymentId = null);

internal static class CardPaymentDtoLocalization
{
    public static string Localize(string arabic, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? arabic
            : english;
}
