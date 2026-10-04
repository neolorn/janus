using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// What carries the admitted messages out of the library's outbox: the one immediate
/// attempt that follows the commit of the transaction that undertook a message, and
/// the passes of the publisher over whatever that attempt did not carry.
/// </summary>
/// <param name="outbox">Where the admitted messages wait.</param>
/// <param name="admission">What judges a retried send again, and releases one that failed for good.</param>
/// <param name="handler">What carries one admitted message.</param>
/// <param name="configuration">Where the retry schedule and the claim's timeout are read.</param>
/// <param name="alerts">Where a spent retry budget's alert goes.</param>
/// <param name="work">The transactions a claim and an outcome are each written in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the retry jitter is drawn from.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, CONV-DESIGN-002, CONV-DESIGN-003, D-022, INF-BG-001 and
/// IDN-PRIN-003. Every row is claimed before the handler is called, by one conditional
/// update committed on its own, and what the attempt made of it is written by one
/// statement conditional on that claim, so the immediate attempt and the passes of any
/// number of processes carry each message once, count each attempt once and record each
/// outcome once. The handler is never called while a transaction is open. A message no
/// handler took is carried again under <c>outbox.retry.*</c>, judged again by the
/// restrictions as they then stand, until its attempts are spent, when its count is
/// released and <c>degradation</c> is raised in the transaction that removes its row.
/// </remarks>
internal sealed class SendPublisher(
    ISendOutbox outbox,
    SendAdmission admission,
    INotificationHandler handler,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : ISendCarrier
{
    // A pass never holds more than this many in memory; the rest wait for the next.
    private const int Batch = 100;

    /// <inheritdoc/>
    /// <remarks>
    /// The transaction that undertook the message has committed. A handler that refuses
    /// or throws leaves the message to the publisher; only a fault of the library's own
    /// (a setting that does not read, a database that does not answer) reaches the
    /// caller, as any fault does.
    /// </remarks>
    public async ValueTask AttemptAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        Schedule schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        (await CarriedAsync(delivery, schedule, cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <summary>
    /// Runs one pass of the publisher over the messages no handler has taken.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many messages the pass carried, or the failure that stopped it.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may deliver what has been committed.</exception>
    public async ValueTask<Result<int>> RetryAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Delivering(context);

        IReadOnlyList<SendDeliveryId> due = await outbox
            .DueAsync(time.GetUtcNow(), Batch, cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return Result.Success(0);
        }

        Error? failure = null;

        Schedule schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Schedule>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        int carried = 0;

        foreach (SendDeliveryId delivery in due)
        {
            bool taken = (await CarriedAsync(delivery, schedule, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            if (taken)
            {
                carried++;
            }
        }

        return Result.Success(carried);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the pass runs as a named
    // principal that may deliver what has been committed, and never as nobody.
    private static SystemPrincipal Delivering(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Delivery)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may deliver what has been committed.",
                nameof(context));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The message is named by its identifier and what it is for: the destination is
    // personal data and an alert travels to channels that are not the recipient's.
    private static Dictionary<string, JsonElement> Exhausted(SendDelivery delivery) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["delivery"] = JsonSerializer.SerializeToElement(delivery.Id.ToString()),
            ["message"] = JsonSerializer.SerializeToElement(WrittenName.Of(delivery.Requested.Message)),
            ["channel"] = JsonSerializer.SerializeToElement(WrittenName.Of(delivery.Requested.Kind)),
            ["attempts"] = JsonSerializer.SerializeToElement(delivery.Attempts),
        };

    // One attempt at one message: claimed, judged again where it is a retry, carried
    // outside any transaction, and settled under the claim. It answers whether the
    // handler took the message.
    private async ValueTask<Result<bool>> CarriedAsync(
        SendDeliveryId id,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        // CONV-DESIGN-003: the claim is one conditional update committed on its own,
        // before the handler is called.
        SendClaim? claimed = (await InUnitAsync(
                async () => Result.Success(await outbox
                    .ClaimAsync(id, now, schedule.ClaimTimeout, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Held<SendClaim?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        if (claimed is not SendClaim claim
            || await outbox.FindAsync(id, cancellationToken).ConfigureAwait(false) is not SendDelivery delivery)
        {
            // Another attempt holds the row, or it is gone: it is that attempt's.
            return Result.Success(false);
        }

        // AUTH-ABUSE-004 AC9: a send carried again is judged by the restrictions as
        // they stand now, with its own count set aside, and counts at this instant where
        // they admit it. One they refuse holds no count and waits as any failed attempt.
        if (delivery.Attempts > 0)
        {
            bool admitted = (await InUnitAsync(
                    () => RetriedAsync(delivery, claim, schedule, now, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref failure));

            if (failure is not null || !admitted)
            {
                return failure is null ? Result.Success(false) : Result.Failure<bool>(failure);
            }
        }

        bool taken = (await TakenAsync(delivery.Admitted, schedule.ClaimTimeout, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => true, _ => false);

        Result settled = (await InUnitAsync(
                async () => (await SettledAsync(delivery, claim, taken, schedule, now, cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => Result.Success(true), Result.Failure<bool>),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(_ => Result.Success(), Result.Failure);

        return settled.Match(() => Result.Success(taken), Result.Failure<bool>);
    }

    // The judgement of a retry, in the unit of work it is settled in where it is
    // refused: whether the restrictions admit the message again.
    private async ValueTask<Result<bool>> RetriedAsync(
        SendDelivery delivery,
        SendClaim claim,
        Schedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<SendPlan>> judged = await admission
            .JudgeAsync(delivery.Requested, weight: 1, delivery.Reference, now, cancellationToken)
            .ConfigureAwait(false);

        if (judged.Match<SendPlan?>(plans => plans[0], _ => null) is SendPlan plan)
        {
            await admission.CountAsync(delivery.Reference, plan, now, cancellationToken).ConfigureAwait(false);

            return Result.Success(true);
        }

        // Refused, it holds none: the gateway floor refuses before the counters are
        // held, so what the send still held is released here.
        await admission.ReleaseAsync(delivery.Reference, cancellationToken).ConfigureAwait(false);

        return (await SettledAsync(delivery, claim, taken: false, schedule, now, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(false), Result.Failure<bool>);
    }

    // The handler is asked outside any transaction, for no longer than the claim
    // stands: an attempt still running then is abandoned as a failed attempt. A handler
    // that throws did not take the message. Letting the fault out would leave the
    // attempt uncounted, so the message would be carried at every pass, never back off
    // and never spend its budget.
    private async ValueTask<Result> TakenAsync(
        SendRequest request,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken)
    {
        using var abandoned = new CancellationTokenSource(claimTimeout, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(abandoned.Token, cancellationToken);

        try
        {
            return await handler.SendAsync(request, either.Token).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

    // What an attempt made of a message, written under the claim it was made under: a
    // message taken is removed (IDN-PRIN-003); one not taken waits for its next attempt,
    // or, its attempts spent, is removed with its count released and its alert raised,
    // or not at all. An outcome whose claim was taken over changes nothing.
    private async ValueTask<Result> SettledAsync(
        SendDelivery delivery,
        SendClaim claim,
        bool taken,
        Schedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (taken)
        {
            _ = await outbox.RemoveAsync(claim, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        SendDelivery refused = delivery.Refused(now, schedule.Initial, schedule.Factor, Jitter());

        if (refused.Attempts < schedule.MaxAttempts)
        {
            _ = await outbox.RecordAsync(refused, claim, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        if (!await outbox.RemoveAsync(claim, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        // AUTH-ABUSE-004: a send that fails for good releases its count and its credit.
        await admission.ReleaseAsync(delivery.Reference, cancellationToken).ConfigureAwait(false);

        return await alerts
            .RaiseAsync(
                Alerts.Scoped(
                    AlertCondition.Degradation,
                    "send:" + WrittenName.Of(delivery.Requested.Kind),
                    now,
                    Exhausted(refused)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // One unit of work of the publisher's own: committed where the work succeeds and
    // rolled back on every other return, a fault included (CONV-DESIGN-003).
    private async ValueTask<Result<TValue>> InUnitAsync<TValue>(
        Func<ValueTask<Result<TValue>>> written,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
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
    // messages refused together are not carried again together.
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
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        TimeSpan claim = (await configuration
                .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts, claim))
            : Result.Failure<Schedule>(failure);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts, TimeSpan ClaimTimeout);
}
