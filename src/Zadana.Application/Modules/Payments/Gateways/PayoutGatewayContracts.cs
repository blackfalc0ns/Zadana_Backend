namespace Zadana.Application.Modules.Payments.Gateways;

/// <summary>
/// Provider-agnostic command to push a payout (vendor or driver) through a payout gateway.
/// </summary>
public sealed record CreatePayoutCommand(
    Guid PayoutId,
    Guid OwnerId,
    string OwnerType,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? BeneficiaryName,
    string? BeneficiaryIban,
    string? BeneficiaryBankCode,
    string? Reference,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record PayoutGatewayResult(
    string ProviderName,
    string? ProviderTransferId,
    string ProviderStatus,
    string? FailureMessage = null,
    string? RawResponse = null);

public sealed record PayoutGatewayDetails(
    string ProviderName,
    string ProviderTransferId,
    string ProviderStatus,
    decimal Amount,
    string Currency,
    DateTime? CompletedAtUtc = null,
    string? FailureMessage = null,
    string? RawResponse = null);
