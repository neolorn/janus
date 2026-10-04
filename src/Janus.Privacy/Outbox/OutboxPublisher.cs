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
/// <param name="ledger">Where an erasure is written down off the host, if the deployment registered one.</param>
/// <param name="configuration">Where the retry schedule and the claim's timeout are read.</param>
/// <param name="alerts">Where a spent retry budget is raised.</param>
/// <param name="work">The transactions a claim, a renewal, a confirmation and an outcome are each written in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-005b, CONV-DESIGN-003, INF-BG-001 and DR-016.
/// Delivery is at least once: a subscriber that confirmed is not offered the event
/// again, and one that did not is, until the budget is spent. A subscriber that throws
/// is a subscriber that did not confirm, and the delivery outlives the process either
/// way. An erasure also waits for its ledger line, which is offered as the first
/// required subscriber, and one completed before the ledger was registered is written
/// down by the pass once. A row is claimed whole before any subscriber is called, so one
/// pass at a time carries it, and an attempt is one pass over the subscribers still to
/// confirm it. No subscriber is called while a transaction is open.
/// </remarks>
internal sealed class OutboxPublisher(
    IOutboxStore outbox,
    IErasureStore erasures,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IErasureLedger? ledger,
    IConfigurationStore configuration,
    IPrivacyAlerts alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    // A pass never holds more than this many completed erasures in memory at once.
    private const int Page = 100;

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many deliveries the pass closed, completed or failed.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may deliver what has been committed.</exception>
    public async ValueTask<int> PublishAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Delivering(context);

        int closed = await DueAsync(cancellationToken).ConfigureAwait(false);

        if (ledger is not null)
        {
            await LedgeredAsync(ledger, cancellationToken).ConfigureAwait(false);
        }

        return closed;
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the pass runs as a named
    // principal that may deliver what has been committed, and never as nobody.
    private static SystemPrincipal Delivering(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Delivery)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may deliver what has been committed.",
                nameof(context));

    private static IReadOnlyList<string> Required(
        IReadOnlyList<ISubjectEventSubscriber> registered) =>
    [
        .. registered.Where(subscriber => subscriber.Required).Select(subscriber => subscriber.Name),
    ];

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

    // DR-016 AC5: an erasure completed before the ledger was registered, by hand
    // included, is written down once. Its row is claimed before the ledger is called, so
    // of two passes one writes the line. Its status, its attempts and its erasures row
    // stand as they are; only the confirmation is recorded, under the claim, which is
    // then released. An erasure this pass did not write down (the append refused, or
    // another pass holding the row) is left for the next pass, and as those stay the
    // oldest still unwritten, the next page passes over exactly them.
    private async ValueTask LedgeredAsync(IErasureLedger registered, CancellationToken cancellationToken)
    {
        var line = new ErasureLedgerSubscriber(registered);
        TimeSpan timeout = await ClaimTimeoutAsync(cancellationToken).ConfigureAwait(false);
        int left = 0;

        while (true)
        {
            IReadOnlyList<DeliveryId> page = await outbox
                .UnledgeredAsync(left, Page, cancellationToken)
                .ConfigureAwait(false);

            foreach (DeliveryId id in page)
            {
                if (!await LedgeredAsync(line, id, timeout, cancellationToken).ConfigureAwait(false))
                {
                    left++;
                }
            }

            if (page.Count < Page)
            {
                return;
            }
        }
    }

    private async ValueTask<bool> LedgeredAsync(
        ErasureLedgerSubscriber line,
        DeliveryId id,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DeliveryClaim? claimed = await InUnitAsync(
                () => outbox.ClaimUnledgeredAsync(id, time.GetUtcNow(), timeout, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        if (claimed is not DeliveryClaim claim)
        {
            return false;
        }

        bool appended = (await outbox.ProgressAsync(id, cancellationToken).ConfigureAwait(false))?.Delivery is Delivery delivery
            && (await HandledAsync(line, delivery.Raised(), timeout, cancellationToken).ConfigureAwait(false))
                .Match(() => true, _ => false);

        return await InUnitAsync(
                async () =>
                {
                    bool written = appended
                        && await outbox
                            .ConfirmAsync(claim, ErasureLedgerSubscriber.Called, time.GetUtcNow(), cancellationToken)
                            .ConfigureAwait(false);

                    _ = await outbox.ReleaseAsync(claim, cancellationToken).ConfigureAwait(false);

                    return written;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<int> DueAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DeliveryId> due = await outbox.DueAsync(time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        Schedule schedule = await ScheduleAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ISubjectEventSubscriber> registered = [.. subscribers];
        IReadOnlyList<ISubjectEventSubscriber> erasing = ErasureLedgerSubscriber.Joined(registered, ledger);
        int closed = 0;

        foreach (DeliveryId id in due)
        {
            if (await DeliveredAsync(id, registered, erasing, schedule, cancellationToken).ConfigureAwait(false))
            {
                closed++;
            }
        }

        return closed;
    }

    // The subscriber is asked outside any transaction, for no longer than the claim
    // stands: one still running then is abandoned as one that did not confirm. A
    // subscriber that throws did not confirm either. Letting the fault out would leave
    // the attempt uncounted, so the delivery would be offered again at every poll, never
    // back off and never spend its budget.
    private async ValueTask<Result> HandledAsync(
        ISubjectEventSubscriber subscriber,
        SubjectEvent raised,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken)
    {
        using var abandoned = new CancellationTokenSource(claimTimeout, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(abandoned.Token, cancellationToken);

        try
        {
            return await subscriber.HandleAsync(raised, either.Token).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(
                ErrorCodes.SystemFault,
                "handler",
                JsonSerializer.SerializeToElement(subscriber.Name)));
        }
    }

    // One attempt of one delivery: the row claimed whole, then one pass over the
    // subscribers still to confirm it, the claim renewed before each, each confirmation
    // written as it happens, and the row's outcome written once, all under the claim.
    // It answers whether the pass closed the delivery.
    private async ValueTask<bool> DeliveredAsync(
        DeliveryId id,
        IReadOnlyList<ISubjectEventSubscriber> registered,
        IReadOnlyList<ISubjectEventSubscriber> erasing,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        // CONV-DESIGN-003: the claim is one conditional update committed on its own,
        // before any subscriber is called.
        DeliveryClaim? claimed = await InUnitAsync(
                () => outbox.ClaimAsync(id, time.GetUtcNow(), schedule.ClaimTimeout, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        // Another pass holds the row, or it is no longer due: it is that pass's.
        if (claimed is not DeliveryClaim claim
            || (await outbox.ProgressAsync(id, cancellationToken).ConfigureAwait(false))?.Delivery is not Delivery delivery)
        {
            return false;
        }

        IReadOnlyList<ISubjectEventSubscriber> waitedFor =
            delivery.Kind is SubjectEventKind.ErasureRequested ? erasing : registered;
        SubjectEvent raised = delivery.Raised();

        foreach (ISubjectEventSubscriber subscriber in waitedFor)
        {
            if (delivery.Confirmed.Contains(subscriber.Name))
            {
                continue;
            }

            // The renewal moves the claim's end to the timeout from now. Where it
            // changes nothing another pass has taken the row over, and this one stops.
            DeliveryClaim? renewed = await InUnitAsync(
                    () => outbox.RenewAsync(claim, time.GetUtcNow(), schedule.ClaimTimeout, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);

            if (renewed is null)
            {
                return false;
            }

            claim = renewed.Value;

            if (!(await HandledAsync(subscriber, raised, schedule.ClaimTimeout, cancellationToken).ConfigureAwait(false))
                .Match(() => true, _ => false))
            {
                continue;
            }

            delivery.Confirm(subscriber.Name);

            if (!await InUnitAsync(
                    () => outbox.ConfirmAsync(claim, subscriber.Name, time.GetUtcNow(), cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return false;
            }
        }

        return await InUnitAsync(
                () => SettledAsync(delivery, claim, waitedFor, schedule, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The row's outcome, written once under the claim the pass was made under: the
    // attempt counted and the next scheduled, the delivery completed where every required
    // subscriber has confirmed, or failed with its alert where the budget is spent. An
    // outcome whose claim was taken over changes nothing.
    private async ValueTask<bool> SettledAsync(
        Delivery delivery,
        DeliveryClaim claim,
        IReadOnlyList<ISubjectEventSubscriber> waitedFor,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        delivery.Attempted(time.GetUtcNow(), schedule.Initial, schedule.Factor, Jitter());

        bool satisfied = delivery.Satisfies(Required(waitedFor));
        bool spent = !satisfied && delivery.Attempts >= schedule.MaxAttempts;

        if (satisfied)
        {
            delivery.Complete();
        }
        else if (spent)
        {
            delivery.Fail();
        }

        if (!await outbox.RecordAsync(delivery, claim, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ErasedAsync(delivery, cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-003a: a spent budget is a diagnostic signal and not somewhere
        // failures go quietly, so it is raised in the transaction that records it.
        if (spent)
        {
            (await alerts
                    .RaiseAsync(
                        AlertCondition.ErasureDeliveryExhausted,
                        scope: null,
                        Exhausted(delivery, waitedFor),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
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

    // One unit of work of the publisher's own: committed where the work returns and
    // rolled back where it faults (CONV-DESIGN-003).
    private async ValueTask<TValue> InUnitAsync<TValue>(
        Func<ValueTask<TValue>> written,
        CancellationToken cancellationToken)
    {
        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        TValue outcome;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            outcome = await written().ConfigureAwait(false);
        }
        catch
        {
            await work.RollbackAsync().ConfigureAwait(false);

            throw;
        }

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return outcome;
    }

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // deliveries failing together do not retry together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    private async ValueTask<TimeSpan> ClaimTimeoutAsync(CancellationToken cancellationToken) =>
        (await configuration
            .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken).ConfigureAwait(false))
        .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private async ValueTask<Schedule> ScheduleAsync(CancellationToken cancellationToken)
    {
        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        return new Schedule(
            initial,
            factor,
            attempts,
            await ClaimTimeoutAsync(cancellationToken).ConfigureAwait(false));
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts, TimeSpan ClaimTimeout);
}
