using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The one path every raised condition takes: written for <c>alert-dispatch</c> inside
/// the transaction that raised it, and announced to the host.
/// </summary>
/// <param name="alerts">Where the condition waits to be handed over.</param>
/// <param name="events">Where the host is told of it.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and chapter 10 section 5b. The condition
/// is handed over from the committed row, so an operation that rolls back raises
/// nothing, and nothing the hand-over needs is built while the operation runs.
/// </remarks>
internal sealed class AlertRaising(IRaisedAlerts alerts, IEvents events, IUnitOfWork work) : IAlertRaising
{
    /// <inheritdoc/>
    public async ValueTask<Result> RaiseAsync(AlertRaised raised, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await alerts.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), cancellationToken)
            .ConfigureAwait(false);

        Result published = await events.PublishAsync(raised, cancellationToken).ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }
}
