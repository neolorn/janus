using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Events;
using Janus.Authentication.Tests;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// The emitted events a test publishes, held in memory, with the claim each row carries.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class PendingEventsInMemory : IPendingEvents
{
    private readonly List<PendingEvent> _held = [];
    private readonly Dictionary<PendingEventId, DateTimeOffset> _claims = [];

    /// <summary>
    /// The unit of work the passes under test run in. Where a test names it, what was
    /// written inside a unit of work that rolled back is put back.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Every event written, as the last write left it.
    /// </summary>
    public IReadOnlyList<PendingEvent> Held => _held;

    /// <summary>
    /// Every claim a pass took, in order.
    /// </summary>
    public List<EventClaim> Claimed { get; } = [];

    /// <summary>
    /// Every renewal that changed a row, in order, as the claim then stood.
    /// </summary>
    public List<EventClaim> Renewed { get; } = [];

    /// <summary>
    /// The consumers each take wrote, in order.
    /// </summary>
    public List<IReadOnlyList<string>> Takes { get; } = [];

    /// <summary>
    /// How many outcomes were written.
    /// </summary>
    public int Outcomes { get; private set; }

    /// <summary>
    /// Whether any write was made while no unit of work was open.
    /// </summary>
    public bool WroteOutsideAUnitOfWork { get; private set; }

    /// <summary>
    /// Stands in for another pass that takes the row over, as one would once the claim
    /// on it had timed out.
    /// </summary>
    /// <param name="pending">The row.</param>
    /// <param name="until">When the claim of the other pass times out.</param>
    public void TakeOver(PendingEventId pending, DateTimeOffset until) => _claims[pending] = until;

    /// <inheritdoc/>
    public ValueTask AddAsync(PendingEvent pending, CancellationToken cancellationToken)
    {
        _held.Add(Copy(pending));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingEventId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingEventId>>(
        [
            .. _held
                .Where(pending => Due(pending, now))
                .OrderBy(pending => pending.Id.Value)
                .Select(pending => pending.Id)
                .Take(count),
        ]);

    /// <inheritdoc/>
    public ValueTask<EventClaim?> ClaimAsync(
        PendingEventId pending,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!_held.Any(held => held.Id == pending && Due(held, now)))
        {
            return ValueTask.FromResult<EventClaim?>(null);
        }

        Writing();

        var claim = new EventClaim(pending, now + timeout);

        _claims[pending] = claim.Until;
        Claimed.Add(claim);

        return ValueTask.FromResult<EventClaim?>(claim);
    }

    /// <inheritdoc/>
    public ValueTask<EventClaim?> RenewAsync(
        EventClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult<EventClaim?>(null);
        }

        Writing();

        EventClaim renewed = claim with { Until = now + timeout };

        _claims[claim.Event] = renewed.Until;
        Renewed.Add(renewed);

        return ValueTask.FromResult<EventClaim?>(renewed);
    }

    /// <inheritdoc/>
    public ValueTask<PendingEvent?> FindAsync(PendingEventId pending, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.SingleOrDefault(held => held.Id == pending) is PendingEvent found ? Copy(found) : null);

    /// <inheritdoc/>
    public ValueTask<bool> TakeAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Writing();

        PendingEvent held = _held.Single(one => one.Id == claim.Event);

        foreach (string consumer in pending.Taken)
        {
            held.Take(consumer);
        }

        Takes.Add([.. pending.Taken.Order(StringComparer.Ordinal)]);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Writing();

        _held[_held.FindIndex(one => one.Id == claim.Event)] = Copy(pending);
        _ = _claims.Remove(claim.Event);
        Outcomes++;

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.RemoveAll(pending => pending.PublishedAt is not null));

    private static PendingEvent Copy(PendingEvent pending) =>
        PendingEvent.Existing(
            pending.Id,
            pending.Raised,
            pending.Attempts,
            pending.NextAttemptAt,
            pending.Taken,
            pending.PublishedAt,
            pending.FailedAt);

    private bool Due(PendingEvent pending, DateTimeOffset now) =>
        pending.PublishedAt is null
        && pending.FailedAt is null
        && pending.NextAttemptAt <= now
        && (!_claims.TryGetValue(pending.Id, out DateTimeOffset until) || until <= now);

    private bool Holds(EventClaim claim) =>
        _claims.TryGetValue(claim.Event, out DateTimeOffset until) && until == claim.Until;

    // What is held now is what a rollback of the unit of work in progress puts back.
    private void Writing()
    {
        if (Work is null)
        {
            return;
        }

        if (!Work.Open)
        {
            WroteOutsideAUnitOfWork = true;

            return;
        }

        List<PendingEvent> held = [.. _held.Select(Copy)];
        var claims = new Dictionary<PendingEventId, DateTimeOffset>(_claims);
        int outcomes = Outcomes;

        Work.Undoing(() =>
        {
            _held.Clear();
            _held.AddRange(held);
            _claims.Clear();

            foreach (KeyValuePair<PendingEventId, DateTimeOffset> one in claims)
            {
                _claims[one.Key] = one.Value;
            }

            Outcomes = outcomes;
        });
    }
}
