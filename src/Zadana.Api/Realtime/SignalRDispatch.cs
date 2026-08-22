using Microsoft.AspNetCore.SignalR;

namespace Zadana.Api.Realtime;

/// <summary>
/// SignalR <c>SendAsync</c> can wait on a stale hub connection or Redis
/// backplane until <see cref="HubOptions.ClientTimeoutInterval"/> (60s).
/// Cap each send so one dead client cannot stall inbox persist, later
/// events, or the HTTP request that triggered the fan-out.
/// </summary>
internal static class SignalRDispatch
{
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    public static async Task SendToGroupAsync<THub>(
        IHubContext<THub> hubContext,
        string groupName,
        string methodName,
        object? payload,
        ILogger logger,
        string operation,
        CancellationToken cancellationToken)
        where THub : Hub
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SendTimeout);

        try
        {
            await hubContext.Clients
                .Group(groupName)
                .SendAsync(methodName, payload, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "SignalR {Operation} timed out after {TimeoutSeconds}s for group {Group} method {Method}.",
                operation,
                SendTimeout.TotalSeconds,
                groupName,
                methodName);
        }
    }
}
