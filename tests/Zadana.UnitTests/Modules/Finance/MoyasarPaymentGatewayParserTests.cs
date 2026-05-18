using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Infrastructure.Services.Payments;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Finance;

/// <summary>
/// Validates that <see cref="MoyasarPaymentGateway.FetchPaymentAsync"/> can parse
/// real Moyasar API responses captured in the test sandbox. The JSON below is a
/// verbatim snapshot from <c>POST /v1/payments</c> with a Visa test card that
/// routed through 3DS (status=initiated) and a synthetic <c>paid</c> variant.
/// </summary>
public class MoyasarPaymentGatewayParserTests
{
    private const string InitiatedJson = """
    {
      "id": "db8970eb-a693-4a12-a004-03c6c3047b88",
      "status": "initiated",
      "amount": 13075,
      "fee": 0,
      "currency": "SAR",
      "refunded": 0,
      "refunded_at": null,
      "captured": 0,
      "captured_at": null,
      "voided_at": null,
      "description": "Order ORD-SMOKE-1",
      "amount_format": "130.75 SAR",
      "invoice_id": null,
      "callback_url": "https://localhost:5298/api/payments/moyasar/verify",
      "created_at": "2026-05-18T15:59:36.583Z",
      "updated_at": "2026-05-18T15:59:36.583Z",
      "metadata": {
        "payment_id": "00000000-0000-0000-0000-000000000002",
        "order_id": "00000000-0000-0000-0000-000000000001",
        "order_number": "ORD-SMOKE-1"
      },
      "source": {
        "type": "creditcard",
        "company": "visa",
        "name": "Test Customer",
        "number": "4111-11XX-XXXX-1111",
        "gateway_id": "moyasar_cc_aKCCmcYLGJtv7EEYiFSWkac",
        "reference_number": null,
        "transaction_url": "https://api.moyasar.com/v1/card_auth/d9424b04-c21b-41bc-8d03-32627a9d8995/prepare",
        "response_code": null
      }
    }
    """;

    private const string PaidJson = """
    {
      "id": "PAY-PAID-001",
      "status": "paid",
      "amount": 13075,
      "fee": 343,
      "currency": "SAR",
      "refunded": 0,
      "captured": 13075,
      "captured_at": "2026-05-18T16:05:11.000Z",
      "description": "Order ORD-SMOKE-1",
      "metadata": {
        "order_id": "00000000-0000-0000-0000-000000000001",
        "payment_id": "00000000-0000-0000-0000-000000000002",
        "order_number": "ORD-SMOKE-1"
      },
      "source": {
        "type": "creditcard",
        "company": "mada",
        "reference_number": "RRN-555-9999"
      }
    }
    """;

    [Fact]
    public async Task FetchPaymentAsync_parses_initiated_payment_from_real_api_snapshot()
    {
        var gateway = BuildGatewayReturning(InitiatedJson);

        var details = await gateway.FetchPaymentAsync("db8970eb-a693-4a12-a004-03c6c3047b88", CancellationToken.None);

        details.ProviderName.Should().Be("Moyasar");
        details.ProviderPaymentId.Should().Be("db8970eb-a693-4a12-a004-03c6c3047b88");
        details.ProviderStatus.Should().Be("initiated");
        details.AmountMinorUnits.Should().Be(13075);
        details.Currency.Should().Be("SAR");
        details.Metadata.Should().ContainKey("order_id").WhoseValue.Should().Be("00000000-0000-0000-0000-000000000001");
        details.Metadata.Should().ContainKey("payment_id").WhoseValue.Should().Be("00000000-0000-0000-0000-000000000002");
        details.Metadata.Should().ContainKey("order_number").WhoseValue.Should().Be("ORD-SMOKE-1");
        details.RawResponse.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FetchPaymentAsync_parses_paid_payment_with_capture_and_rrn()
    {
        var gateway = BuildGatewayReturning(PaidJson);

        var details = await gateway.FetchPaymentAsync("PAY-PAID-001", CancellationToken.None);

        details.ProviderStatus.Should().Be("paid");
        details.AmountMinorUnits.Should().Be(13075);
        details.Currency.Should().Be("SAR");
        details.ProviderReferenceNumber.Should().Be("RRN-555-9999");
        details.CapturedAtUtc.Should().NotBeNull();
        details.CapturedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task FetchPaymentAsync_throws_when_disabled()
    {
        var gateway = BuildGatewayWith(new MoyasarSettings
        {
            Enabled = false,
            BaseUrl = "https://api.moyasar.com/v1/",
            PublishableKey = "pk_test_dummy",
            SecretKey = "sk_test_dummy",
        }, _ => CreateResponse(HttpStatusCode.OK, "{}"));

        var act = async () => await gateway.FetchPaymentAsync("any-id", CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "PAYMENT_UNAVAILABLE");
    }

    [Fact]
    public async Task FetchPaymentAsync_propagates_provider_error_status()
    {
        var gateway = BuildGatewayReturning("""{"errors":["payment not found"]}""", HttpStatusCode.NotFound);

        var act = async () => await gateway.FetchPaymentAsync("missing", CancellationToken.None);

        await act.Should().ThrowAsync<ExternalServiceException>()
            .Where(x => x.ErrorCode == "MOYASAR_FETCH_FAILED");
    }

    private static MoyasarPaymentGateway BuildGatewayReturning(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return BuildGatewayWith(
            new MoyasarSettings
            {
                Enabled = true,
                BaseUrl = "https://api.moyasar.com/v1/",
                PublishableKey = "pk_test_dummy",
                SecretKey = "sk_test_dummy",
                EnabledMethods = ["creditcard", "applepay", "stcpay"],
                SupportedNetworks = ["mada", "visa", "mastercard"],
                Currency = "SAR",
            },
            _ => CreateResponse(status, body));
    }

    private static MoyasarPaymentGateway BuildGatewayWith(MoyasarSettings settings, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri(settings.BaseUrl),
        };
        return new MoyasarPaymentGateway(
            http,
            Options.Create(settings),
            NullLogger<MoyasarPaymentGateway>.Instance);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
