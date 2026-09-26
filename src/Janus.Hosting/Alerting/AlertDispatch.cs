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
/// <param name="work">The one transaction each condition is carried in.</param>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and chapter 10 section 5b. A condition
/// leaves the table in the transaction the router records its delivery in, so one the
/// router refused is carried on a later pass and none is carried twice.
/// </remarks>
internal sealed class AlertDispatch(IRaisedAlerts alerts, AlertRouter router, IUnitOfWork work)
{
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
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            Result<AlertDelivery> delivered = await router.RaiseAsync(alert.Raised, cancellationToken)
                .ConfigureAwait(false);

            if (delivered.Match(_ => (Error?)null, error => error) is Error undelivered)
            {
                return Result.Failure<int>(undelivered);
            }

            await alerts.RemoveAsync(alert.Id, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            carried++;
        }

        return Result.Success(carried);
    }
}
