using FluentAssertions;
using Zadana.Api.Modules.Payments.Controllers;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymentWebhook;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class MoyasarWebhookTests
{
    [Fact]
    public void Validate_AcceptsSecretTokenJsonWithWhitespace()
    {
        var payload = """
        {
          "id": "evt_1",
          "type": "payment_paid",
          "secret_token": "whsec_test",
          "data": { "id": "pay_1" }
        }
        """;

        var valid = MoyasarWebhookSecretValidator.Validate(
            configuredSecret: "whsec_test",
            payload: payload,
            providedSignature: null,
            allowMissingSecret: false);

        valid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsInvalidSecretToken()
    {
        var payload = """
        {
          "id": "evt_1",
          "type": "payment_paid",
          "secret_token": "wrong",
          "data": { "id": "pay_1" }
        }
        """;

        var valid = MoyasarWebhookSecretValidator.Validate(
            configuredSecret: "whsec_test",
            payload: payload,
            providedSignature: null,
            allowMissingSecret: false);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentWebhook_QueuesEventInsteadOfConfirmingInline()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new ProcessPaymentWebhookCommandHandler(context);
        var providerPaymentId = Guid.NewGuid().ToString();
        var payload = $$"""
        {
          "id": "evt_{{Guid.NewGuid():N}}",
          "type": "payment_paid",
          "secret_token": "whsec_test",
          "data": { "id": "{{providerPaymentId}}" }
        }
        """;

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                Provider: "Moyasar",
                Payload: payload,
                SecretValid: true,
                Headers: null),
            CancellationToken.None);

        result.Message.Should().Be("queued");
        result.Status.Should().Be("received");

        var inbox = context.PaymentProviderEvents.Single();
        inbox.ProviderPaymentId.Should().Be(providerPaymentId);
        inbox.Status.Should().Be(PaymentProviderEventStatus.Received);
        inbox.ProcessingAttempts.Should().Be(0);
    }
}
