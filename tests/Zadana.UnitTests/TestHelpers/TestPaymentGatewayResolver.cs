using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;

namespace Zadana.UnitTests.TestHelpers;

/// <summary>
/// Minimal stub used by handler tests after the Paymob removal. Returns a
/// fake "Moyasar" gateway that reports <see cref="IPaymentGateway.IsEnabled"/>
/// as configured. The fake throws on <c>CreateSession/Fetch/Refund</c> so any
/// test that accidentally exercises the network path will fail loud.
/// </summary>
public sealed class TestPaymentGatewayResolver : IPaymentGatewayResolver
{
    private readonly IReadOnlyList<IPaymentGateway> _gateways;

    public TestPaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways)
    {
        _gateways = gateways.ToList();
    }

    public static TestPaymentGatewayResolver Enabled(string providerName = "Moyasar") =>
        new([new FakePaymentGateway(providerName, isEnabled: true)]);

    public static TestPaymentGatewayResolver Disabled(string providerName = "Moyasar") =>
        new([new FakePaymentGateway(providerName, isEnabled: false)]);

    public IPaymentGateway Resolve(string providerName)
    {
        if (!TryResolve(providerName, out var gateway) || gateway is null)
        {
            throw new InvalidOperationException($"No gateway registered for {providerName}.");
        }
        return gateway;
    }

    public bool TryResolve(string providerName, out IPaymentGateway? gateway)
    {
        gateway = _gateways.FirstOrDefault(g =>
            string.Equals(g.ProviderName, providerName, StringComparison.OrdinalIgnoreCase) && g.IsEnabled);
        return gateway is not null;
    }

    public IReadOnlyList<IPaymentGateway> GetEnabledGateways() =>
        _gateways.Where(g => g.IsEnabled).ToList();

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public FakePaymentGateway(string providerName, bool isEnabled)
        {
            ProviderName = providerName;
            IsEnabled = isEnabled;
        }

        public string ProviderName { get; }
        public bool IsEnabled { get; }

        public Task<CreatePaymentSessionResult> CreateSessionAsync(CreatePaymentSessionCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException("FakePaymentGateway is for IsEnabled checks only.");

        public Task<GatewayPaymentDetails> FetchPaymentAsync(string providerPaymentId, CancellationToken cancellationToken)
            => throw new NotSupportedException("FakePaymentGateway is for IsEnabled checks only.");

        public Task<RefundGatewayResult> RefundAsync(RefundGatewayCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException("FakePaymentGateway is for IsEnabled checks only.");
    }
}
