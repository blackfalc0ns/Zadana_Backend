using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services.Payments;

/// <summary>
/// Default <see cref="IPaymentGatewayResolver"/> backed by an
/// <see cref="IEnumerable{T}"/> registration of <see cref="IPaymentGateway"/>.
/// Disabled gateways are filtered out of <see cref="GetEnabledGateways"/>
/// so admin/feature-flag changes propagate without DI rewiring.
/// </summary>
public sealed class PaymentGatewayResolver : IPaymentGatewayResolver
{
    private readonly IReadOnlyList<IPaymentGateway> _gateways;

    public PaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways)
    {
        _gateways = gateways.ToList();
    }

    public IPaymentGateway Resolve(string providerName)
    {
        if (!TryResolve(providerName, out var gateway) || gateway is null)
        {
            throw new BusinessRuleException(
                "PAYMENT_PROVIDER_UNAVAILABLE",
                $"Payment provider '{providerName}' is not registered or is disabled.");
        }

        return gateway;
    }

    public bool TryResolve(string providerName, out IPaymentGateway? gateway)
    {
        gateway = _gateways.FirstOrDefault(g =>
            string.Equals(g.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)
            && g.IsEnabled);

        return gateway is not null;
    }

    public IReadOnlyList<IPaymentGateway> GetEnabledGateways() =>
        _gateways.Where(g => g.IsEnabled).ToList();
}
