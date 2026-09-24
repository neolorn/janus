using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// Where a raised condition goes: to the destinations the deployment configured, by
/// the library's own alert channels, and to the host as the <see cref="AlertRaised"/>
/// event.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and chapter 10 section 5b. The alert
/// channels are the library's consumer of the event, not the host's, so a condition
/// reaches the operator whatever the host does with the event: nobody has to be
/// watching. They carry it once the transaction that raised it has committed.
/// </remarks>
internal interface IAlertChannels
{
    /// <summary>
    /// Records one raised condition for the alert channels and announces it to the
    /// host, inside the transaction in progress.
    /// </summary>
    /// <param name="raised">What fired.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>Nothing, or the failure where the host did not take the event.</returns>
    ValueTask<Result> RaiseAsync(AlertRaised raised, CancellationToken cancellationToken);
}
