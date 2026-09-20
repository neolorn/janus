using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// Where the emitted events go, holding them in order so a test can read what an
/// operation announced and what it did not.
/// </summary>
internal sealed class EventsInMemory : IEvents
{
    /// <summary>
    /// Every event published, in order.
    /// </summary>
    public List<JanusEvent> Published { get; } = [];

    /// <summary>
    /// The published events of one kind, in order.
    /// </summary>
    /// <typeparam name="TEvent">The kind.</typeparam>
    /// <returns>Those events.</returns>
    public IReadOnlyList<TEvent> Of<TEvent>()
        where TEvent : JanusEvent =>
        [.. Published.OfType<TEvent>()];

    /// <inheritdoc/>
    public ValueTask PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : JanusEvent
    {
        Published.Add(raised);

        return ValueTask.CompletedTask;
    }
}
