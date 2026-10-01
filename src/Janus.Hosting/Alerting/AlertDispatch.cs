using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;

namespace Janus.Hosting.Alerting;

/// <summary>
/// One pass of the alert channels: the conditions raised in transactions that have
/// committed, carried by the router to the destinations, oldest first.
/// </summary>
/// <param name="alerts">Where the raised conditions wait.</param>
/// <param name="router">What carries one to the destinations.</param>
/// <param name="work">The transaction each condition leaves the table in.</param>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and chapter 10 section 5b. A condition
/// leaves the table once the router has carried it, so one the router refused is
/// carried on a later pass, and one carried but not yet removed is deduplicated rather
/// than carried twice.
/// </remarks>
internal sealed class AlertDispatch(IRaisedAlerts alerts, AlertRouter router, IUnitOfWork work)
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
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many conditions were carried, or the refusal that stopped the pass.</returns>
    public async ValueTask<Result<int>> CarryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RaisedAlert> waiting = await alerts.OldestAsync(Batch, cancellationToken)
            .ConfigureAwait(false);

        int carried = 0;

        foreach (RaisedAlert alert in waiting)
        {
            // D-022: the router commits its claim and carries the condition outside any
            // transaction; a row left behind by a pass that stops here is folded into
            // that claim by the deduplication ledger on the next pass.
            Result<AlertDelivery> delivered = await router.RaiseAsync(alert.Raised, cancellationToken)
                .ConfigureAwait(false);

            if (delivered.Match(_ => (Error?)null, error => error) is Error undelivered)
            {
                return Result.Failure<int>(undelivered);
            }

            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notBegun)
            {
                return Result.Failure<int>(notBegun);
            }

            await alerts.RemoveAsync(alert.Id, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommitted)
            {
                return Result.Failure<int>(notCommitted);
            }

            carried++;
        }

        return Result.Success(carried);
    }
}
