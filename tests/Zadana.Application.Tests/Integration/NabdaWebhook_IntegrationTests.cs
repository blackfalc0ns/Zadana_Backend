using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

public class NabdaWebhook_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public NabdaWebhook_IntegrationTests(ZadanaWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Receive_WithValidPayload_DoesNotRequireAuthentication()
    {
        var payload = new
        {
            instanceId = "a82a8cd3-e60b-4635-baf7-db783ecc7e2c",
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

        var response = await _client.PostAsJsonAsync("/api/webhooks/nabda", payload);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        content.Should().Contain("Nabda");
        content.Should().Contain("message.ack");
    }
}
