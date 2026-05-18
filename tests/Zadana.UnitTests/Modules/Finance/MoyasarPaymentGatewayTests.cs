using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Services.Payments;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Finance;

public class MoyasarPaymentGatewayTests
{
    [Fact]
    public void IsEnabled_is_true_when_keys_are_configured()
    {
        var gateway = BuildGateway(enabled: true);
        gateway.IsEnabled.Should().BeTrue();
        gateway.ProviderName.Should().Be("Moyasar");
    }

    [Fact]
    public void IsEnabled_is_false_when_disabled_flag_off()
    {
        var gateway = BuildGateway(enabled: false);
        gateway.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_is_false_when_keys_missing()
    {
        var settings = new MoyasarSettings
        {
            Enabled = true,
            BaseUrl = "https://api.moyasar.com/v1/",
            PublishableKey = string.Empty,
            SecretKey = string.Empty,
        };
        var gateway = BuildGatewayWith(settings);
        gateway.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateSessionAsync_returns_provider_config_for_card_channel()
    {
        var gateway = BuildGateway(enabled: true);

        var result = await gateway.CreateSessionAsync(
            new CreatePaymentSessionCommand(
                OrderId: Guid.NewGuid(),
                PaymentId: Guid.NewGuid(),
                Channel: PaymentMethodChannel.Card,
                Amount: 130.75m,
                Currency: "SAR",
                Description: "Order ORD-1",
                CallbackUrl: "https://callback",
                IdempotencyKey: "test-key",
                Metadata: new Dictionary<string, string> { ["order_number"] = "ORD-1" }),
            CancellationToken.None);

        result.ProviderName.Should().Be("Moyasar");
        result.ClientAction.Should().Be("RenderMoyasarForm");
        result.RawCreateResponse.Should().NotBeNullOrEmpty();
        result.ProviderConfig.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSessionAsync_rejects_non_sar_currency()
    {
        var gateway = BuildGateway(enabled: true);

        var act = async () => await gateway.CreateSessionAsync(
            new CreatePaymentSessionCommand(
                OrderId: Guid.NewGuid(),
                PaymentId: Guid.NewGuid(),
                Channel: PaymentMethodChannel.Card,
                Amount: 100m,
                Currency: "USD",
                Description: "Order",
                CallbackUrl: "https://callback",
                IdempotencyKey: "test-key"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "UNSUPPORTED_CURRENCY");
    }

    private static MoyasarPaymentGateway BuildGateway(bool enabled)
    {
        return BuildGatewayWith(new MoyasarSettings
        {
            Enabled = enabled,
            BaseUrl = "https://api.moyasar.com/v1/",
            PublishableKey = "pk_test_dummy",
            SecretKey = "sk_test_dummy",
            WebhookSecret = "whsec_dummy",
            EnabledMethods = ["creditcard", "applepay", "samsungpay", "stcpay"],
            SupportedNetworks = ["mada", "visa", "mastercard"],
            Currency = "SAR",
        });
    }

    private static MoyasarPaymentGateway BuildGatewayWith(MoyasarSettings settings)
    {
        var http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl) };
        return new MoyasarPaymentGateway(
            http,
            Options.Create(settings),
            NullLogger<MoyasarPaymentGateway>.Instance);
    }
}
