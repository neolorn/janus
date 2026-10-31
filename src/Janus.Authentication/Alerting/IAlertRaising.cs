using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The one way a condition is raised: written, with its <see cref="AlertRaised"/>
/// event row, in the transaction that raised it.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, OPS-ALERT-003, CONV-DESIGN-002 and chapter 10 section 5b.
/// <c>alert-dispatch</c> hands the written condition to the host's alert handler once
/// that transaction has committed, so nobody has to be watching and a transaction that
/// rolls back raises nothing.
/// </remarks>
internal interface IAlertRaising
{
    /// <summary>
    /// Records one raised condition for <c>alert-dispatch</c> and announces it to the
    /// host, inside the transaction in progress.
    /// </summary>
    /// <param name="raised">What fired.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>Nothing, or the failure where the host did not take the event.</returns>
    ValueTask<Result> RaiseAsync(AlertRaised raised, CancellationToken cancellationToken);

    /// <summary>
    /// Records one raised condition for <c>alert-dispatch</c> under the identifier its
    /// caller drew, and announces it to the host, inside the transaction in progress: a
    /// caller that also hands the condition over itself hands it as this raise
    /// (OPS-ALERT-003 AC3).
    /// </summary>
    /// <param name="alert">What fired, and the identifier it is raised under.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>Nothing, or the failure where the host did not take the event.</returns>
    ValueTask<Result> RaiseAsync(RaisedAlert alert, CancellationToken cancellationToken);
}
