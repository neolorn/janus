using System;
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

    /// <summary>
    /// What a test does while the consumer holds an event.
    /// </summary>
    public Action? Meanwhile { get; set; }

    /// <summary>
    /// Whether it never answers, until whoever offered the event gives up on it.
    /// </summary>
    public bool Stalls { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(AccountRegistered raised, CancellationToken cancellationToken) =>
        TakenAsync(raised, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(AccountSuspended raised, CancellationToken cancellationToken) =>
        TakenAsync(raised, cancellationToken);

    private async ValueTask<Result> TakenAsync(DomainEvent raised, CancellationToken cancellationToken)
    {
        Received.Add(raised);
        Meanwhile?.Invoke();

        if (Stalls)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return Result.Success();
    }
}
