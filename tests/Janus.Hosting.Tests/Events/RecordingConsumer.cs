using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// A host's consumer of two kinds of event that takes every one it is offered.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class RecordingConsumer : IEventConsumer<AccountRegistered>, IEventConsumer<AccountSuspended>
{
    /// <summary>
    /// Every event it was offered, in order.
    /// </summary>
    public List<DomainEvent> Received { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(AccountRegistered raised, CancellationToken cancellationToken) =>
        Taken(raised);

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(AccountSuspended raised, CancellationToken cancellationToken) =>
        Taken(raised);

    private ValueTask<Result> Taken(DomainEvent raised)
    {
        Received.Add(raised);

        return ValueTask.FromResult(Result.Success());
    }
}
