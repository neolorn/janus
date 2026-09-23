using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// Where the emitted events go, holding them in order so a test can read what an
/// operation announced and what it did not.
/// </summary>
internal sealed class EventsInMemory : IEvents
{
    /// <summary>
    /// Every event published, in order.
    /// </summary>
    public List<DomainEvent> Published { get; } = [];

    /// <summary>
    /// The published events of one kind, in order.
    /// </summary>
    /// <typeparam name="TEvent">The kind.</typeparam>
    /// <returns>Those events.</returns>
    public IReadOnlyList<TEvent> Of<TEvent>()
        where TEvent : DomainEvent =>
        [.. Published.OfType<TEvent>()];

    /// <summary>
    /// What the publication answers with instead of taking the event, where a test
    /// stands in for a consumer that would not take it.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        Published.Add(raised);

        return ValueTask.FromResult(Result.Success());
    }
}
