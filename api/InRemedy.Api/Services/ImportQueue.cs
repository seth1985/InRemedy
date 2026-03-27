using System.Threading.Channels;

namespace InRemedy.Api.Services;

public sealed class ImportQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>();

    public ValueTask QueueAsync(Guid importBatchId, CancellationToken cancellationToken)
        => channel.Writer.WriteAsync(importBatchId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => channel.Reader.ReadAsync(cancellationToken);
}
