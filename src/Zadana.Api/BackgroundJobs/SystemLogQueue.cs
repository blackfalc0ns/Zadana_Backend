using System.Threading.Channels;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// In-memory bounded channel that decouples the SystemLogMiddleware (which
/// runs on the request thread) from the actual SQL INSERT (which happens on
/// a background worker). Bounded so a runaway flood drops the oldest entries
/// instead of unbounded memory growth or back-pressuring requests.
/// </summary>
public interface ISystemLogQueue
{
    /// <summary>Tries to enqueue an entry. Returns false if the queue is full.</summary>
    bool TryEnqueue(SystemLogEntry entry);

    /// <summary>Reads the underlying channel for the worker.</summary>
    ChannelReader<SystemLogEntry> Reader { get; }
}

public sealed class SystemLogQueue : ISystemLogQueue
{
    private readonly Channel<SystemLogEntry> _channel = Channel.CreateBounded<SystemLogEntry>(
        new BoundedChannelOptions(capacity: 5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<SystemLogEntry> Reader => _channel.Reader;

    public bool TryEnqueue(SystemLogEntry entry) => _channel.Writer.TryWrite(entry);
}
