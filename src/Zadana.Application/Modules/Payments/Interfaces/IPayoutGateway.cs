using Zadana.Application.Modules.Payments.Gateways;

namespace Zadana.Application.Modules.Payments.Interfaces;

/// <summary>
/// Provider-agnostic interface for outbound transfers (vendor/driver payouts).
/// Section 15 of the spec keeps payout providers strictly independent from
/// payment-collection providers, so this is intentionally split from
/// <see cref="IPaymentGateway"/>.
/// </summary>
public interface IPayoutGateway
{
    string ProviderName { get; }

    bool IsEnabled { get; }

    Task<PayoutGatewayResult> CreatePayoutAsync(CreatePayoutCommand command, CancellationToken cancellationToken);

    Task<PayoutGatewayDetails> FetchPayoutAsync(string providerTransferId, CancellationToken cancellationToken);
}
