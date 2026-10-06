using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Events;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Events;

/// <summary>
/// One pass of the publisher over the emitted events: each one whose transaction has
/// committed is offered to every consumer registered for its kind that has not yet
/// taken it, and the row is marked once they all have.
/// </summary>
/// <param name="events">Where the events wait.</param>
/// <param name="consumers">Who the host registered for each kind.</param>
/// <param name="configuration">Where the retry schedule and the claim's timeout are read.</param>
/// <param name="alerts">Where a spent budget's alert goes.</param>
/// <param name="work">The transactions a claim, a renewal, a take and an outcome are each written in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002, CONV-DESIGN-003, IDN-LIFE-003a, INF-BG-001
/// and D-162 item 29. Delivery is at least once: a consumer that took the event is not
/// offered it again, and one that did not is, under <c>outbox.retry.*</c>, until the
/// budget is spent and <c>degradation</c> is raised. A row is claimed whole before any
/// consumer is called, so one pass at a time carries it, and an attempt is one pass over
/// the consumers still to take it. No consumer is called while a transaction is open.
/// </remarks>
internal sealed class EventPublisher(
    IPendingEvents events,
    EventConsumers consumers,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    // A pass never holds more than this many in memory; the rest wait for the next.
    private const int Batch = 100;

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many events every consumer has now taken, or the failure that stopped the pass.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may deliver what has been committed.</exception>
    public async ValueTask<Result<int>> PublishAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Delivering(context);

        IReadOnlyList<PendingEventId> due = await events
            .DueAsync(time.GetUtcNow(), Batch, cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return Result.Success(0);
        }

        Error? failure = null;

        Schedule schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<Schedule>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        int published = 0;

        foreach (PendingEventId pending in due)
        {
            bool marked = (await OfferedAsync(pending, schedule, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            if (marked)
            {
                published++;
            }
        }

        return Result.Success(published);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the pass runs as a named
    // principal that may deliver what has been committed, and never as nobody.
    private static SystemPrincipal Delivering(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Delivery)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may deliver what has been committed.",
                nameof(context));

    // The consumers are named by their types, which are the host's code and carry no
    // one's data.
    private static Dictionary<string, JsonElement> Exhausted(
        PendingEvent pending,
        IReadOnlyList<EventConsumer> registered) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["event"] = JsonSerializer.SerializeToElement(pending.Id.ToString()),
            ["kind"] = JsonSerializer.SerializeToElement(pending.Raised.GetType().Name),
            ["attempts"] = JsonSerializer.SerializeToElement(pending.Attempts),
            ["outstanding"] = JsonSerializer.SerializeToElement(
                registered
                    .Where(consumer => !pending.Taken.Contains(consumer.Name))
                    .Select(consumer => consumer.Name)),
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // One attempt of one event: the row claimed whole, then one pass over the consumers
    // still to take it, the claim renewed before each, each take written as it happens,
    // and the row's outcome written once, all under the claim. It answers whether every
    // consumer has now taken the event.
    private async ValueTask<Result<bool>> OfferedAsync(
        PendingEventId id,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        // CONV-DESIGN-003: the claim is one conditional update committed on its own,
        // before any consumer is called.
        EventClaim? claimed = (await InUnitAsync(
                async () => Result.Success(await events
                    .ClaimAsync(id, time.GetUtcNow(), schedule.ClaimTimeout, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Withheld<EventClaim?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        // Another pass holds the row, or it is no longer due: it is that pass's.
        if (claimed is not EventClaim claim
            || await events.FindAsync(id, cancellationToken).ConfigureAwait(false) is not PendingEvent pending)
        {
            return Result.Success(false);
        }

        IReadOnlyList<EventConsumer> registered = consumers.Of(pending.Raised);

        foreach (EventConsumer consumer in registered)
        {
            if (pending.Taken.Contains(consumer.Name))
            {
                continue;
            }

            // The renewal moves the claim's end to the timeout from now. Where it
            // changes nothing another pass has taken the row over, and this one stops.
            EventClaim? renewed = (await InUnitAsync(
                    async () => Result.Success(await events
                        .RenewAsync(claim, time.GetUtcNow(), schedule.ClaimTimeout, cancellationToken)
                        .ConfigureAwait(false)),
                    cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, error => Withheld<EventClaim?>(error, ref failure));

            if (failure is not null || renewed is null)
            {
                return failure is null ? Result.Success(false) : Result.Failure<bool>(failure);
            }

            claim = renewed.Value;

            if (!(await TakenAsync(consumer, schedule.ClaimTimeout, cancellationToken).ConfigureAwait(false))
                .Match(() => true, _ => false))
            {
                continue;
            }

            pending.Take(consumer.Name);

            bool written = (await InUnitAsync(
                    async () => Result.Success(await events
                        .TakeAsync(pending, claim, cancellationToken)
                        .ConfigureAwait(false)),
                    cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, error => Withheld<bool>(error, ref failure));

            if (failure is not null || !written)
            {
                return failure is null ? Result.Success(false) : Result.Failure<bool>(failure);
            }
        }

        return await InUnitAsync(
                () => SettledAsync(pending, claim, registered, schedule, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The row's outcome, written once under the claim the pass was made under: marked
    // where every consumer has taken the event, or the attempt counted and the next
    // scheduled, or, the budget spent, failed with its alert or not at all. An outcome
    // whose claim was taken over changes nothing.
    private async ValueTask<Result<bool>> SettledAsync(
        PendingEvent pending,
        EventClaim claim,
        IReadOnlyList<EventConsumer> registered,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        bool marked = registered.All(consumer => pending.Taken.Contains(consumer.Name));
        bool spent = false;

        if (marked)
        {
            pending.Published(now);
        }
        else
        {
            spent = pending.Refused(now, schedule.Initial, schedule.Factor, schedule.MaxAttempts, Jitter());
        }

        if (!await events.RecordAsync(pending, claim, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        if (!spent)
        {
            return Result.Success(marked);
        }

        // IDN-LIFE-003a: a spent budget is a diagnostic signal and not somewhere
        // failures go quietly, so it is recorded with the alert or not at all. It is
        // raised under the event's kind: a consumer that fails one event of a kind
        // fails the rest, and OPS-ALERT-002 keeps that to one alert.
        return (await alerts
                .RaiseAsync(
                    Alerts.Of(
                        AlertCondition.Degradation,
                        "event:" + pending.Raised.GetType().Name,
                        now,
                        Exhausted(pending, registered)),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(false), Result.Failure<bool>);
    }

    // The consumer is asked outside any transaction, for no longer than the claim
    // stands: one still running then is abandoned as one that did not take the event. A
    // consumer that throws did not take it either. Letting the fault out would leave the
    // attempt uncounted, so the event would be offered at every pass, never back off
    // and never spend its budget.
    private async ValueTask<Result> TakenAsync(
        EventConsumer consumer,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken)
    {
        using var abandoned = new CancellationTokenSource(claimTimeout, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(abandoned.Token, cancellationToken);

        try
        {
            return await consumer.Handle(either.Token).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

    // One unit of work of the publisher's own: committed where the work succeeds and
    // rolled back on every other return, a fault included (CONV-DESIGN-003).
    private async ValueTask<Result<TValue>> InUnitAsync<TValue>(
        Func<ValueTask<Result<TValue>>> written,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<TValue>(notBegun);
        }

        Result<TValue> outcome = await FaultRolledBackAsync(written, cancellationToken).ConfigureAwait(false);

        if (outcome.Match(_ => (Error?)null, error => error) is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return outcome;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => outcome, Result.Failure<TValue>);
    }

    private async ValueTask<Result<TValue>> FaultRolledBackAsync<TValue>(
        Func<ValueTask<Result<TValue>>> written,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await written().ConfigureAwait(false);
        }
        catch
        {
            await work.RollbackAsync().ConfigureAwait(false);

            throw;
        }
    }

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // events failing together do not retry together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    private async ValueTask<Result<Schedule>> ScheduleAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<decimal>(error, ref failure));

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        TimeSpan claim = (await configuration
                .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts, claim))
            : Result.Failure<Schedule>(failure);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts, TimeSpan ClaimTimeout);
}
