using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Infrastructure.Services.Payments;
using Zadana.Infrastructure.Settings;

namespace Zadana.UnitTests.Modules.Finance;

public class MoyasarPayoutGatewayTests
{
    [Fact]
    public void IsEnabled_requires_payouts_source_and_secret_key()
    {
        BuildGateway(BuildSettings(enabled: true, sourceId: "src_123")).IsEnabled.Should().BeTrue();
        BuildGateway(BuildSettings(enabled: false, sourceId: "src_123")).IsEnabled.Should().BeFalse();
        BuildGateway(BuildSettings(enabled: true, sourceId: "")).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePayoutAsync_posts_bank_destination_with_minor_units_and_auth()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var gateway = BuildGateway(
            BuildSettings(enabled: true, sourceId: "src_123"),
            request =>
            {
                capturedRequest = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return CreateResponse(HttpStatusCode.Created, """
                {
                  "id": "pout_123",
                  "status": "queued",
                  "amount": 13075,
                  "currency": "SAR",
                  "sequence_number": "1234567890123456"
                }
                """);
            });

        var result = await gateway.CreatePayoutAsync(
            new CreatePayoutCommand(
                PayoutId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                OwnerId: Guid.NewGuid(),
                OwnerType: "Vendor",
                Amount: 130.75m,
                Currency: "SAR",
                IdempotencyKey: "payout:test",
                BeneficiaryName: "Vendor Name",
                BeneficiaryIban: "sa1234567890123456789012",
                BeneficiaryBankCode: "Bank",
                Reference: "ref-1",
                Metadata: new Dictionary<string, string> { ["payout_id"] = "payout-1" },
                BeneficiaryMobile: "0500000000",
                BeneficiaryCountry: "SA",
                BeneficiaryCity: "Riyadh",
                Purpose: "payment_to_merchant",
                SequenceNumber: "1234567890123456"),
            CancellationToken.None);

        result.ProviderName.Should().Be("Moyasar");
        result.ProviderTransferId.Should().Be("pout_123");
        result.ProviderStatus.Should().Be("queued");
        result.ProviderSequenceNumber.Should().Be("1234567890123456");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().EndWith("/payouts");
        capturedRequest.Headers.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("sk_test_dummy:"))));

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        root.GetProperty("source_id").GetString().Should().Be("src_123");
        root.GetProperty("amount").GetInt64().Should().Be(13075);
        root.GetProperty("purpose").GetString().Should().Be("payment_to_merchant");
        root.GetProperty("sequence_number").GetString().Should().Be("1234567890123456");
        root.GetProperty("destination").GetProperty("type").GetString().Should().Be("bank");
        root.GetProperty("destination").GetProperty("iban").GetString().Should().Be("SA1234567890123456789012");
        root.GetProperty("destination").GetProperty("name").GetString().Should().Be("Vendor Name");
        root.GetProperty("destination").GetProperty("mobile").GetString().Should().Be("0500000000");
    }

    [Fact]
    public async Task CreatePayoutAsync_keeps_transient_provider_failures_unknown()
    {
        var gateway = BuildGateway(
            BuildSettings(enabled: true, sourceId: "src_123"),
            _ => CreateResponse(HttpStatusCode.GatewayTimeout, """
            {
              "message": "upstream timeout"
            }
            """));

        var result = await gateway.CreatePayoutAsync(
            new CreatePayoutCommand(
                PayoutId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                OwnerId: Guid.NewGuid(),
                OwnerType: "Vendor",
                Amount: 10m,
                Currency: "SAR",
                IdempotencyKey: "payout:test",
                BeneficiaryName: "Vendor Name",
                BeneficiaryIban: "SA1234567890123456789012",
                BeneficiaryBankCode: "Bank",
                Reference: "ref-1",
                BeneficiaryMobile: "0500000000",
                BeneficiaryCountry: "SA",
                BeneficiaryCity: "Riyadh",
                SequenceNumber: "1234567890123456"),
            CancellationToken.None);

        result.ProviderStatus.Should().Be("unknown");
        result.IsTransient.Should().BeTrue();
        result.ProviderSequenceNumber.Should().Be("1234567890123456");
    }

    [Fact]
    public async Task FetchPayoutAsync_parses_paid_payout_details()
    {
        var gateway = BuildGateway(
            BuildSettings(enabled: true, sourceId: "src_123"),
            _ => CreateResponse(HttpStatusCode.OK, """
            {
              "id": "pout_paid",
              "status": "paid",
              "amount": 5000,
              "currency": "SAR",
              "sequence_number": "0000000000000001",
              "updated_at": "2026-05-19T08:00:00Z"
            }
            """));

        var details = await gateway.FetchPayoutAsync("pout_paid", CancellationToken.None);

        details.ProviderName.Should().Be("Moyasar");
        details.ProviderTransferId.Should().Be("pout_paid");
        details.ProviderStatus.Should().Be("paid");
        details.Amount.Should().Be(50m);
        details.Currency.Should().Be("SAR");
        details.ProviderSequenceNumber.Should().Be("0000000000000001");
        details.CompletedAtUtc.Should().NotBeNull();
    }

    private static MoyasarSettings BuildSettings(bool enabled, string sourceId) =>
        new()
        {
            BaseUrl = "https://api.moyasar.com/v1/",
            SecretKey = "sk_test_dummy",
            Payouts = new MoyasarPayoutSettings
            {
                Enabled = enabled,
                SourceId = sourceId,
                DefaultCountry = "SA",
                DefaultCity = "Riyadh",
                VendorPurpose = "payment_to_merchant",
                DriverPurpose = "payroll_benefits",
                PollingIntervalSeconds = 300
            }
        };

    private static MoyasarPayoutGateway BuildGateway(MoyasarSettings settings) =>
        BuildGateway(settings, _ => CreateResponse(HttpStatusCode.OK, "{}"));

    private static MoyasarPayoutGateway BuildGateway(MoyasarSettings settings, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri(settings.BaseUrl),
        };

        return new MoyasarPayoutGateway(
            http,
            Options.Create(settings),
            NullLogger<MoyasarPayoutGateway>.Instance);
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
