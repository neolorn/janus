using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Storage.Tests;

/// <summary>
/// Where the emitted events go, where a test reads none of them from the database.
/// </summary>
internal sealed class EventsInMemory : IEvents
{
    /// <summary>
    /// Every event published, in order.
    /// </summary>
    public List<DomainEvent> Published { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result> PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        Published.Add(raised);

        return ValueTask.FromResult(Result.Success());
    }
}
