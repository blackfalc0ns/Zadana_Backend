using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Payments.Gateways;

/// <summary>
/// Provider-agnostic command to create a payment session/intent.
/// </summary>
public sealed record CreatePaymentSessionCommand(
    Guid OrderId,
    Guid PaymentId,
    PaymentMethodChannel Channel,
    decimal Amount,
    string Currency,
    string Description,
    string CallbackUrl,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null,
    string? CustomerFullName = null);

/// <summary>
/// Provider-agnostic result of <c>CreateSessionAsync</c>. The shape of
/// <see cref="ProviderConfig"/> is provider-specific JSON (e.g. publishableKey,
/// iframe url, sdk parameters) and is forwarded to the client unchanged.
/// </summary>
public sealed record CreatePaymentSessionResult(
    string ProviderName,
    string? ProviderPaymentId,
    string? ProviderInvoiceId,
    string ClientAction,
    object? ProviderConfig,
    string? RawCreateResponse);

/// <summary>
/// Snapshot of a payment fetched from the gateway. Used during webhook/callback
/// verification to reconcile against the local <c>Payment</c> + <c>Order</c>.
/// </summary>
public sealed record GatewayPaymentDetails(
    string ProviderName,
    string ProviderPaymentId,
    string ProviderStatus,
    long AmountMinorUnits,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata,
    string? ProviderInvoiceId = null,
    string? ProviderReferenceNumber = null,
    DateTime? CapturedAtUtc = null,
    string? RawResponse = null,
    string? FailureMessage = null);

public sealed record RefundGatewayCommand(
    Guid RefundId,
    string ProviderPaymentId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? Reason = null);

public sealed record RefundGatewayResult(
    string ProviderName,
    string? ProviderRefundId,
    string ProviderStatus,
    decimal Amount,
    string Currency,
    string? FailureMessage = null,
    string? RawResponse = null);
