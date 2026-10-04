using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Alerting;

/// <summary>
/// One pass of the alert channels: the conditions raised in transactions that have
/// committed, carried by the router to the destinations, oldest first.
/// </summary>
/// <param name="alerts">Where the raised conditions wait.</param>
/// <param name="router">What carries one to the destinations.</param>
/// <param name="configuration">Where the claim's timeout is read.</param>
/// <param name="work">The transactions a claim and an outcome are each written in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002, CONV-DESIGN-003, INF-BG-001 and chapter 10
/// section 5b. Every raised condition is claimed before the router is called, by one
/// conditional update committed on its own, and leaves the table by one statement
/// conditional on that claim, so passes in any number of processes carry each once. A
/// condition the router refused gives up its claim and is carried on a later pass.
/// </remarks>
internal sealed class AlertDispatch(
    IRaisedAlerts alerts,
    AlertRouter router,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The name of the job that runs the passes, whose own lapse the worker delivers
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
            // before the router is called; a row another pass holds is that pass's.
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

            // D-022: the router commits its deduplication claim and carries the condition
            // outside any transaction.
            Result<AlertDelivery> delivered = await router.RaiseAsync(alert.Raised, cancellationToken)
                .ConfigureAwait(false);

            bool refused = delivered.Match(_ => false, _ => true);

            // The outcome is written under the claim: a condition carried leaves the
            // table, and one the router refused gives up its claim for the next pass.
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

            if (delivered.Match(_ => (Error?)null, error => error) is Error undelivered)
            {
                return Result.Failure<int>(undelivered);
            }

            if (settled)
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

    // One unit of work of the pass's own: committed where the work succeeds and rolled
    // back on every other return, a fault included (CONV-DESIGN-003).
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
}
