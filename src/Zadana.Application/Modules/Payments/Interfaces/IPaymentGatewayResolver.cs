namespace Zadana.Application.Modules.Payments.Interfaces;

/// <summary>
/// Resolves an <see cref="IPaymentGateway"/> implementation by provider name
/// (e.g. "Moyasar"). Throws when the provider is unknown or disabled.
/// </summary>
public interface IPaymentGatewayResolver
{
    IPaymentGateway Resolve(string providerName);

    bool TryResolve(string providerName, out IPaymentGateway? gateway);

    IReadOnlyList<IPaymentGateway> GetEnabledGateways();
}
