using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;

namespace Zadana.Application.Tests.Application.Payments;

public class PaymobGatewayTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_ShouldBuildIframeUrlFromProviderResponses()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """{"token":"auth-token"}""",
            HttpStatusCode.OK,
            """{"id":998119}""",
            HttpStatusCode.OK,
            """{"token":"payment-key-123"}""");

        var gateway = CreateGateway(handler);

        var result = await gateway.CreateCheckoutSessionAsync(
            new PaymobCheckoutSessionRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "ORD-123",
                123.45m,
                "EGP",
                [new PaymobOrderItemRequest("Fresh Cabbage", "Fresh Cabbage", 2, 45.00m)],
                "Paymob",
                "Customer",
                "paymob@test.com",
                "01000000000",
                "Nasr City",
                "Cairo",
                "EG"));

        result.ProviderReference.Should().Be("998119");
        result.IframeUrl.Should().Be("https://accept.paymob.com/api/acceptance/iframes/998119?payment_token=payment-key-123");
    }

    [Fact]
    public void ParseWebhookNotification_WhenHmacIsValid_ShouldReturnSuccessPayload()
    {
        const string secret = "hmac-secret";
        const string transactionJson = """
        {
          "id": 777,
          "pending": false,
          "success": true,
          "amount_cents": 10000,
          "created_at": "2026-04-16T12:00:00Z",
          "currency": "EGP",
          "error_occured": false,
          "has_parent_transaction": false,
          "integration_id": 555,
          "is_3d_secure": false,
          "is_auth": false,
          "is_capture": false,
          "is_refunded": false,
          "is_standalone_payment": true,
          "is_voided": false,
          "owner": 1,
          "source_data": {
            "pan": "2346",
            "sub_type": "MasterCard",
            "type": "card"
          },
          "order": {
            "id": 998119,
            "merchant_order_id": "11111111-1111-1111-1111-111111111111"
          }
        }
        """;
        var hmac = ComputeHmac(transactionJson, secret);
        var payload = $$"""
        {
          "type": "TRANSACTION",
          "hmac": "{{hmac}}",
          "obj": {{transactionJson}}
        }
        """;

        var gateway = CreateGateway(new StubHttpMessageHandler(), secret);

        var result = gateway.ParseWebhookNotification(payload);

        result.PaymentId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.ProviderReference.Should().Be("998119");
        result.ProviderTransactionId.Should().Be("777");
        result.IsSuccess.Should().BeTrue();
    }

    private static PaymobGateway CreateGateway(HttpMessageHandler handler, string hmacSecret = "")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://accept.paymob.com")
        };

        return new PaymobGateway(
            httpClient,
            Options.Create(new PaymobSettings
            {
                Enabled = true,
                ApiKey = "api-key",
                IframeId = 998119,
                IntegrationId = 555,
                HmacSecret = hmacSecret,
                BaseUrl = "https://accept.paymob.com",
                Currency = "EGP"
            }),
            NullLogger<PaymobGateway>.Instance);
    }

    private static string ComputeHmac(string transactionJson, string secret)
    {
        using var document = JsonDocument.Parse(transactionJson);
        var transaction = document.RootElement;
        var order = transaction.GetProperty("order");
        var sourceData = transaction.GetProperty("source_data");

        var concatenated =
            transaction.GetProperty("amount_cents").GetRawText() +
            transaction.GetProperty("created_at").GetString() +
            transaction.GetProperty("currency").GetString() +
            "false" +
            "false" +
            transaction.GetProperty("id").GetRawText() +
            transaction.GetProperty("integration_id").GetRawText() +
            "false" +
            "false" +
            "false" +
            "false" +
            "true" +
            "false" +
            order.GetProperty("id").GetRawText() +
            transaction.GetProperty("owner").GetRawText() +
            "false" +
            sourceData.GetProperty("pan").GetString() +
            sourceData.GetProperty("sub_type").GetString() +
            sourceData.GetProperty("type").GetString() +
            "true";

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public StubHttpMessageHandler(params object[] responses)
        {
            for (var i = 0; i < responses.Length; i += 2)
            {
                _responses.Enqueue(new HttpResponseMessage((HttpStatusCode)responses[i])
                {
                    Content = new StringContent((string)responses[i + 1], Encoding.UTF8, "application/json")
                });
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responses.Dequeue());
    }
}
