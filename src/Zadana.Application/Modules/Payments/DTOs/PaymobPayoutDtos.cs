namespace Zadana.Application.Modules.Payments.DTOs;

public sealed record PaymobPayoutRequest(
    Guid PayoutId,
    decimal Amount,
    string CurrencyCode,
    string DestinationType,
    string DestinationSnapshot,
    string TransferReference);

public sealed record PaymobPayoutResult(
    bool IsAccepted,
    string? ProviderTransferId,
    string? TransferReference,
    string? FailureReason,
    string? RawPayload);

public sealed record PaymobPayoutWebhookNotification(
    string ProviderTransferId,
    string Status,
    string? TransferReference,
    string? FailureReason,
    string RawPayload);
