using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

public class WapilotWebhook_IntegrationTests : IClassFixture<WapilotWebhookWebFactory>
{
    internal const string WebhookSecret = "test-wapilot-webhook-secret";
    private readonly HttpClient _client;

    public WapilotWebhook_IntegrationTests(WapilotWebhookWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Receive_WhenWebhookSecretNotConfigured_ShouldReturnUnauthorized()
    {
        using var factory = new ZadanaWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/webhooks/wapilot", new { @event = "message.ack" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Receive_WithValidPayloadAndSecret_DoesNotRequireAuthentication()
    {
        var payload = new
        {
            @event = "message.ack",
            payload = new
            {
                messageId = Guid.NewGuid().ToString(),
                status = "SENT",
                phone = "+201012345678",
                message = "Your OTP is 1234"
            },
            timestamp = DateTimeOffset.UtcNow
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/wapilot")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Wapilot-Webhook-Secret", WebhookSecret);

        var response = await _client.SendAsync(request);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        content.Should().Contain("WAPIlot");
        content.Should().Contain("message.ack");
    }
}

public sealed class WapilotWebhookWebFactory : ZadanaWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("WapilotOtp:WebhookSecret", WapilotWebhook_IntegrationTests.WebhookSecret);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WapilotOtp:WebhookSecret"] = WapilotWebhook_IntegrationTests.WebhookSecret
            });
        });
    }
}
