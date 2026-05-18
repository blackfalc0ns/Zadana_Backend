using Zadana.Application.Modules.Payments.Gateways;

namespace Zadana.Application.Modules.Payments.Interfaces;

/// <summary>
/// Provider-agnostic interface for online payment collection (Moyasar today,
/// other providers later). Order- and payment-flow code MUST go through this
/// abstraction; resolve a concrete implementation via
/// <see cref="IPaymentGatewayResolver"/>.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Stable provider key, e.g. "Moyasar". Case-sensitive.</summary>
    string ProviderName { get; }

    /// <summary>True when configuration permits live calls to this provider.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates a payment session/intent and returns provider config for the client SDK.</summary>
    Task<CreatePaymentSessionResult> CreateSessionAsync(CreatePaymentSessionCommand command, CancellationToken cancellationToken);

    /// <summary>Fetches the authoritative payment state from the provider for verification.</summary>
    Task<GatewayPaymentDetails> FetchPaymentAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>Issues a refund against an existing provider payment.</summary>
    Task<RefundGatewayResult> RefundAsync(RefundGatewayCommand command, CancellationToken cancellationToken);
}
