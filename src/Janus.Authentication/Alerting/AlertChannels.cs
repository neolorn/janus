using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The one path every raised condition takes: written for the alert channels inside
/// the transaction that raised it, and announced to the host.
/// </summary>
/// <param name="alerts">Where the condition waits for the channels.</param>
/// <param name="events">Where the host is told of it.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and chapter 10 section 5b. The channels
/// carry the condition from the committed row, so an operation that rolls back raises
/// nothing, and nothing the channels need is built while the operation runs.
/// </remarks>
internal sealed class AlertChannels(IRaisedAlerts alerts, IEvents events, IUnitOfWork work) : IAlertChannels
{
    /// <inheritdoc/>
    public async ValueTask<Result> RaiseAsync(AlertRaised raised, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await alerts.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), cancellationToken)
            .ConfigureAwait(false);

        Result published = await events.PublishAsync(raised, cancellationToken).ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
