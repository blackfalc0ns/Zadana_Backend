using Microsoft.AspNetCore.Http.Connections;

namespace Zadana.Api.Configuration;

public static class RealtimeTransportConfiguration
{
    public static HttpTransportType Resolve(
        bool webSocketsEnabled,
        bool serverSentEventsEnabled)
    {
        var transports = HttpTransportType.LongPolling;

        if (serverSentEventsEnabled)
        {
            transports |= HttpTransportType.ServerSentEvents;
        }

        if (webSocketsEnabled)
        {
            transports |= HttpTransportType.WebSockets;
        }

        return transports;
    }
}
