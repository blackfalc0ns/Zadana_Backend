using System.Threading.Channels;
using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Api.BackgroundJobs;

public class AdminMasterProductBulkOperationQueue : IAdminMasterProductBulkOperationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid operationId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(operationId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
