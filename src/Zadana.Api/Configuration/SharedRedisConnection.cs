using StackExchange.Redis;

namespace Zadana.Api.Configuration;

/// <summary>
/// Lazily creates one Redis multiplexer shared by distributed cache, output
/// cache, and the SignalR backplane. StackExchange.Redis multiplexers are
/// designed to be long-lived and shared across callers.
/// </summary>
public sealed class SharedRedisConnection : IAsyncDisposable
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _connection;

    public SharedRedisConnection(string connectionString, string clientName)
    {
        var configuration = ConfigurationOptions.Parse(connectionString, ignoreUnknown: true);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = Math.Max(configuration.ConnectRetry, 3);
        configuration.ConnectTimeout = Math.Max(configuration.ConnectTimeout, 5000);
        configuration.SyncTimeout = Math.Max(configuration.SyncTimeout, 5000);
        configuration.AsyncTimeout = Math.Max(configuration.AsyncTimeout, 5000);
        configuration.KeepAlive = configuration.KeepAlive > 0 ? configuration.KeepAlive : 30;
        configuration.ClientName ??= clientName;

        _connection = new Lazy<Task<IConnectionMultiplexer>>(
            async () => await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<IConnectionMultiplexer> GetConnectionAsync() => _connection.Value;

    public Task<IConnectionMultiplexer> GetConnectionAsync(TextWriter _) => _connection.Value;

    public async ValueTask DisposeAsync()
    {
        if (!_connection.IsValueCreated)
        {
            return;
        }

        var connection = await _connection.Value.ConfigureAwait(false);
        await connection.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
        connection.Dispose();
    }
}
