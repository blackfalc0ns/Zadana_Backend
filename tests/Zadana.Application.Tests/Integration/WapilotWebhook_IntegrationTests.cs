using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

public class WapilotWebhook_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public WapilotWebhook_IntegrationTests(ZadanaWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Receive_WithValidPayload_DoesNotRequireAuthentication()
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

        var response = await _client.PostAsJsonAsync("/api/webhooks/wapilot", payload);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        content.Should().Contain("WAPIlot");
        content.Should().Contain("message.ack");
    }
}
