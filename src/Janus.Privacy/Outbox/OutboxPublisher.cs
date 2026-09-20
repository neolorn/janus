using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Outbox;

/// <summary>
/// One pass of the outbox worker: every delivery whose attempt is due, offered to
/// each subscriber that has not confirmed it.
/// </summary>
/// <param name="outbox">Where the deliveries are.</param>
/// <param name="erasures">Where an erasure's own row is carried in step with its delivery.</param>
/// <param name="subscribers">Who the host registered.</param>
/// <param name="configuration">Where the retry schedule is read.</param>
/// <param name="alerts">Where a spent retry budget is raised.</param>
/// <param name="work">The one transaction a pass records its progress in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements IDN-LIFE-003a and PRIV-RIGHT-005b. Delivery is at least once: a
/// subscriber that confirmed is not offered the event again, and one that did not is,
/// until the budget is spent. A subscriber that throws is a subscriber that did not
/// confirm, and the delivery outlives the process either way.
/// </remarks>
internal sealed class OutboxPublisher(
    IOutboxStore outbox,
    IErasureStore erasures,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IConfigurationStore configuration,
    IPrivacyAlerts alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many deliveries the pass closed, completed or failed.</returns>
    public async ValueTask<int> PublishAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        IReadOnlyList<Delivery> due = await outbox.DueAsync(now, cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        Schedule schedule = await ScheduleAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ISubjectEventSubscriber> registered = [.. subscribers];
        int closed = 0;

        foreach (Delivery delivery in due)
        {
            if (await DeliveredAsync(delivery, registered, schedule, now, cancellationToken)
                .ConfigureAwait(false))
            {
                closed++;
            }
        }

        return closed;
    }

    /// <summary>
    /// Closes a delivery whose retry budget was spent, by the hand of an operator who
    /// has done the work the subscriber could not.
    /// </summary>
    /// <param name="delivery">Which delivery.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the refusal.</returns>
    /// <remarks>
    /// IDN-LIFE-003a: the manual path exists for permanent failure and is itself
    /// recorded, so a delivery never leaves the outbox without a trace of who ended
    /// it.
    /// </remarks>
    public async ValueTask<Result> CompleteAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken)
    {
        if (await outbox.FindAsync(delivery, cancellationToken).ConfigureAwait(false)
            is not Delivery held)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (held.Status is not ErasureStatus.Failed)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        held.CompleteManually();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await outbox.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await ClosedAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // A subscriber that throws is a subscriber that did not confirm. Letting the
    // fault out would leave the attempt uncounted, so the delivery would be offered
    // again at every poll, never back off and never spend its budget.
    private static async ValueTask<Result> HandledAsync(
        ISubjectEventSubscriber subscriber,
        SubjectEvent raised,
        CancellationToken cancellationToken)
    {
        try
        {
            return await subscriber.HandleAsync(raised, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            return Result.Failure(Error.From(
                ErrorCodes.SystemFault,
                "handler",
                JsonSerializer.SerializeToElement(subscriber.Name)));
        }
    }

    private static IReadOnlyList<string> Required(
        IReadOnlyList<ISubjectEventSubscriber> registered) =>
    [
        .. registered.Where(subscriber => subscriber.Required).Select(subscriber => subscriber.Name),
    ];

    private async ValueTask<bool> DeliveredAsync(
        Delivery delivery,
        IReadOnlyList<ISubjectEventSubscriber> registered,
        Schedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SubjectEvent raised = delivery.Raised();

        foreach (ISubjectEventSubscriber subscriber in registered)
        {
            if (delivery.Confirmed.Contains(subscriber.Name))
            {
                continue;
            }

            if ((await HandledAsync(subscriber, raised, cancellationToken).ConfigureAwait(false))
                .Match(() => true, _ => false))
            {
                delivery.Confirm(subscriber.Name);
            }
        }

        delivery.Attempted(now, schedule.Initial, schedule.Factor, Jitter());

        bool satisfied = delivery.Satisfies(Required(registered));
        bool spent = !satisfied && delivery.Attempts >= schedule.MaxAttempts;

        if (satisfied)
        {
            delivery.Complete();
        }
        else if (spent)
        {
            delivery.Fail();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await outbox.RecordAsync(delivery, cancellationToken).ConfigureAwait(false);
        await ErasedAsync(delivery, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-003a: a spent budget is a diagnostic signal and not somewhere
        // failures go quietly, so it is raised the moment it is recorded.
        if (spent)
        {
            await alerts
                .RaiseAsync(
                    AlertCondition.ErasureDeliveryExhausted,
                    scope: null,
                    Exhausted(delivery, registered),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return satisfied || spent;
    }

    // The erasure's own row says how far the host-side work has got, so it carries
    // what the delivery carries and never a second answer to the same question.
    private async ValueTask ErasedAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Kind is not SubjectEventKind.ErasureRequested
            || await erasures.FindBySubjectAsync(delivery.Subject, cancellationToken)
                .ConfigureAwait(false) is not Erasure erasure
            || erasure.Status is not ErasureStatus.AwaitingSubscribers)
        {
            return;
        }

        while (erasure.Attempts < delivery.Attempts)
        {
            erasure.RecordAttempt();
        }

        if (delivery.Status is ErasureStatus.Complete)
        {
            erasure.Complete();
        }
        else if (delivery.Status is ErasureStatus.Failed)
        {
            erasure.Fail();
        }

        await erasures.RecordAsync(erasure, cancellationToken).ConfigureAwait(false);
    }

    // The erasure's own row is closed by the same hand: it followed the delivery
    // into failure, so it follows it out rather than describing work that is done.
    private async ValueTask ClosedAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Kind is not SubjectEventKind.ErasureRequested
            || await erasures.FindBySubjectAsync(delivery.Subject, cancellationToken)
                .ConfigureAwait(false) is not Erasure erasure
            || erasure.Status is not ErasureStatus.Failed)
        {
            return;
        }

        erasure.CompleteManually();

        await erasures.RecordAsync(erasure, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, JsonElement> Exhausted(
        Delivery delivery,
        IReadOnlyList<ISubjectEventSubscriber> registered) =>
        new Dictionary<string, JsonElement>(capacity: 4, StringComparer.Ordinal)
        {
            ["delivery"] = JsonSerializer.SerializeToElement(delivery.Id.ToString()),
            ["kind"] = JsonSerializer.SerializeToElement(delivery.Kind.ToString()),
            ["attempts"] = JsonSerializer.SerializeToElement(delivery.Attempts),
            ["outstanding"] = JsonSerializer.SerializeToElement(
                registered
                    .Where(subscriber => subscriber.Required
                        && !delivery.Confirmed.Contains(subscriber.Name))
                    .Select(subscriber => subscriber.Name)),
        };

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // deliveries failing together do not retry together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    private async ValueTask<Schedule> ScheduleAsync(CancellationToken cancellationToken)
    {
        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => TimeSpan.FromSeconds(30));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => 2.0m);

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => 10);

        return new Schedule(initial, factor, attempts);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts);
}
