using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Alerting;

/// <summary>
/// One pass of <c>alert-dispatch</c>: the conditions raised in transactions that have
/// committed, handed to the host's alert handler, oldest first, one alert per condition
/// per window rather than one per occurrence.
/// </summary>
/// <param name="alerts">Where the raised conditions wait.</param>
/// <param name="ledger">What remembers which conditions were already handed over.</param>
/// <param name="handler">The host's alert handler, which delivers what it takes.</param>
/// <param name="configuration">Where the claim's timeout and the window are read.</param>
/// <param name="work">The transactions a claim and an outcome are each written in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-ALERT-001, OPS-ALERT-002, OPS-ALERT-003, CONV-DESIGN-002,
/// CONV-DESIGN-003, INF-BG-001 and chapter 10 section 5b. Every raised condition is
/// claimed before the handler is called, by one conditional update committed on its own,
/// and leaves the table by one statement conditional on that claim, so passes in any
/// number of processes carry each once. Its deduplication key is claimed the same way
/// before the handler is called, for that raise, so two passes that reach one condition
/// inside one window hand it over once. A condition the handler did not take, a handler
/// that threw among them, gives the key back and its claim up, and is handed over again
/// on a later pass; one whose pass ended before the handler answered finds the key
/// claimed for itself and unanswered, which does not count against it. A hand-over the
/// handler took is recorded as taken whatever an earlier, abandoned hand-over of the same
/// raise does after it. The library delivers no alert itself.
/// </remarks>
internal sealed class AlertDispatch(
    IRaisedAlerts alerts,
    IAlertLedger ledger,
    IAlertHandler handler,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The name of the job that runs the passes, whose own lapse the worker hands over
    /// without waiting for it.
    /// </summary>
    public const string Job = "alert-dispatch";

    // A pass never holds more than this many in memory; the rest wait for the next.
    private const int Batch = 100;

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many conditions were carried, or the refusal that stopped the pass.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may deliver what has been committed.</exception>
    public async ValueTask<Result<int>> CarryAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Delivering(context);

        IReadOnlyList<RaisedAlert> waiting = await alerts
            .OldestAsync(time.GetUtcNow(), Batch, cancellationToken)
            .ConfigureAwait(false);

        if (waiting.Count == 0)
        {
            return Result.Success(0);
        }

        Error? failure = null;

        TimeSpan timeout = (await configuration
                .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        int carried = 0;

        foreach (RaisedAlert alert in waiting)
        {
            // CONV-DESIGN-003: the claim is one conditional update committed on its own,
            // before the handler is called; a row another pass holds is that pass's.
            DateTimeOffset now = time.GetUtcNow();

            DateTimeOffset? claimed = (await InUnitAsync(
                    async () => Result.Success(await alerts
                        .ClaimAsync(alert.Id, now, timeout, cancellationToken)
                        .ConfigureAwait(false)),
                    cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, error => Held<DateTimeOffset?>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            if (claimed is not DateTimeOffset claim)
            {
                continue;
            }

            Result handed = await HandedOverAsync(alert, claim, cancellationToken).ConfigureAwait(false);

            bool refused = handed.Match(() => false, _ => true);

            // The outcome is written under the claim: a condition carried leaves the
            // table, and one the handler did not take gives up its claim for the next pass.
            bool settled = (await InUnitAsync(
                    async () => Result.Success(refused
                        ? await alerts.ReleaseAsync(alert.Id, claim, cancellationToken).ConfigureAwait(false)
                        : await alerts.RemoveAsync(alert.Id, claim, cancellationToken).ConfigureAwait(false)),
                    cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            if (handed.Match(() => (Error?)null, error => error) is Error untaken)
            {
                return Result.Failure<int>(untaken);
            }

            if (settled)
            {
                carried++;
            }
        }

        return Result.Success(carried);
    }

    /// <summary>
    /// Hands one raised condition to the host's alert handler, unless one under its
    /// deduplication key was handed over inside the window.
    /// </summary>
    /// <param name="alert">What fired, as it was raised.</param>
    /// <param name="cancellationToken">Abandons the hand-over.</param>
    /// <returns>
    /// Nothing where the handler took it or it was folded into one already handed over,
    /// or the failure where the handler did not take it.
    /// </returns>
    /// <exception cref="ArgumentNullException">The alert is absent.</exception>
    /// <remarks>
    /// OPS-ALERT-002, D-022: the key is claimed for the raise by one conditional write
    /// committed before the handler is called, since no handler is called while a
    /// transaction is open; it is recorded as taken where the handler took the alert, and
    /// given back where it did not, so the raise's next hand-over is the first of the
    /// window again. A raise is never folded into a hand-over of its own that no handler
    /// answered (OPS-ALERT-003 AC2, D-225). The worker hands the lapse of this job over
    /// here directly, as the raise it wrote, so a stalled carrier still reports itself and
    /// the row is folded into the hand-over once the handler has taken it (OPS-ALERT-003
    /// AC3).
    /// </remarks>
    public async ValueTask<Result> HandOverAsync(RaisedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        TimeSpan timeout = (await configuration
                .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        return failure is null
            ? await HandedOverAsync(alert, now + timeout, cancellationToken).ConfigureAwait(false)
            : Result.Failure(failure);
    }

    // One hand-over whose handler is abandoned when the given instant comes: the lapse
    // of the claim it is made under.
    private async ValueTask<Result> HandedOverAsync(
        RaisedAlert alert,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AlertingDedupeWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        string key = Alerts.Deduplication(alert.Raised.IdempotencyKey);

        bool first = (await InUnitAsync(
                async () => Result.Success(await ledger
                    .FirstAsync(key, alert.Id, alert.Raised.RaisedAt, window, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (!first)
        {
            return Result.Success();
        }

        Result taken = await TakenAsync(alert.Raised, until, cancellationToken).ConfigureAwait(false);
        Error? untaken = taken.Match(() => (Error?)null, error => error);

        _ = (await InUnitAsync(
                async () => Result.Success(untaken is null
                    ? await ledger.TakenAsync(key, alert.Id, alert.Raised.RaisedAt, cancellationToken).ConfigureAwait(false)
                    : await ledger.ForgetAsync(key, alert.Id, cancellationToken).ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return untaken is null ? Result.Success() : Result.Failure(untaken);
    }

    // The handler is asked outside any transaction, and its token is cancelled when the
    // claim lapses: a hand-over still running then is abandoned as one not taken, and
    // none is begun once the claim has lapsed. A handler that ignores its token outlasts
    // the claim, and the hand-over is then at least once: another pass may claim the
    // alert and hand it over meanwhile. A handler that throws did not take the alert; its
    // fault is kept with the failure as the worker keeps a job's (BFF-ERR-002 AC2), and
    // letting it out would leave the key claimed and the alert folded into a hand-over
    // nobody answered (OPS-ALERT-003 AC2, D-225).
    private async ValueTask<Result> TakenAsync(AlertRaised raised, DateTimeOffset until, CancellationToken cancellationToken)
    {
        TimeSpan left = until - time.GetUtcNow();

        if (left <= TimeSpan.Zero)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }

        using var abandoned = new CancellationTokenSource(left, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(abandoned.Token, cancellationToken);

        try
        {
            return await handler.TakeAsync(raised, either.Token).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(FaultLog.Failure(fault));
        }
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

    // One unit of work of the pass's own: committed where the work succeeds and rolled
    // back on every other return, a fault included (CONV-DESIGN-003).
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
}
