using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Zadana.Api.Configuration;

namespace Zadana.Application.Tests.Infrastructure;

public sealed class RealtimeTransportConfigurationTests
{
    [Fact]
    public void Resolve_WhenStreamingTransportsDisabled_ReturnsLongPollingOnly()
    {
        var transports = RealtimeTransportConfiguration.Resolve(
            webSocketsEnabled: false,
            serverSentEventsEnabled: false);

        transports.Should().Be(HttpTransportType.LongPolling);
    }

    [Fact]
    public void Resolve_WhenAllTransportsEnabled_ReturnsAllSupportedTransports()
    {
        var transports = RealtimeTransportConfiguration.Resolve(
            webSocketsEnabled: true,
            serverSentEventsEnabled: true);

        transports.Should().HaveFlag(HttpTransportType.WebSockets);
        transports.Should().HaveFlag(HttpTransportType.ServerSentEvents);
        transports.Should().HaveFlag(HttpTransportType.LongPolling);
    }
}
