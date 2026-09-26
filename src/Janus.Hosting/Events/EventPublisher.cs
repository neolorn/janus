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
/// <param name="configuration">Where the retry schedule is read.</param>
/// <param name="alerts">Where a spent budget's alert goes.</param>
/// <param name="work">The one transaction each event's progress is recorded in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002, IDN-LIFE-003a, INF-BG-001 and D-162 item 29.
/// Delivery is at least once: a consumer that took the event is not offered it again,
/// and one that did not is, under <c>outbox.retry.*</c>, until the budget is spent and
/// <c>degradation</c> is raised.
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
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many events every consumer has now taken, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> PublishAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        IReadOnlyList<PendingEvent> due = await events.DueAsync(now, Batch, cancellationToken)
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

        foreach (PendingEvent pending in due)
        {
            IReadOnlyList<EventConsumer> registered = consumers.Of(pending.Raised);

            foreach (EventConsumer consumer in registered)
            {
                if (!pending.Taken.Contains(consumer.Name)
                    && (await TakenAsync(consumer, cancellationToken).ConfigureAwait(false))
                        .Match(() => true, _ => false))
                {
                    pending.Take(consumer.Name);
                }
            }

            bool spent = false;

            if (registered.All(consumer => pending.Taken.Contains(consumer.Name)))
            {
                pending.Published(now);
                published++;
            }
            else
            {
                spent = pending.Refused(
                    now,
                    schedule.Initial,
                    schedule.Factor,
                    schedule.MaxAttempts,
                    Jitter());
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await events.RecordAsync(pending, cancellationToken).ConfigureAwait(false);

            // IDN-LIFE-003a: a spent budget is a diagnostic signal and not somewhere
            // failures go quietly, so it is recorded with the alert or not at all. It
            // is raised under the event's kind: a consumer that fails one event of a
            // kind fails the rest, and OPS-ALERT-002 keeps that to one alert.
            if (spent
                && (await alerts
                        .RaiseAsync(
                            Alerts.Of(
                                AlertCondition.Degradation,
                                "event:" + pending.Raised.GetType().Name,
                                now,
                                Exhausted(pending, registered)),
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unalerted)
            {
                return Result.Failure<int>(unalerted);
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(published);
    }

    // A consumer that throws is a consumer that did not take the event. Letting the
    // fault out would leave the attempt uncounted, so the event would be offered at
    // every pass, never back off and never spend its budget.
    private static async ValueTask<Result> TakenAsync(
        EventConsumer consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await consumer.Handle(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

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

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts))
            : Result.Failure<Schedule>(failure);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts);
}
