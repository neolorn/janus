using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// Where the conditions already raised are remembered.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>Implements OPS-ALERT-002 and CONV-DESIGN-003.</remarks>
internal sealed class AlertLedger(StoreContext context) : IAlertLedger
{
    /// <inheritdoc/>
    public async ValueTask<bool> FirstAsync(
        string key,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        AlertRecord? standing = await context.Alerts
            .FindAsync([key], cancellationToken)
            .ConfigureAwait(false);

        if (standing is null)
        {
            context.Alerts.Add(new AlertRecord { Key = key, At = at });

            return true;
        }

        if (standing.At > at - window)
        {
            return false;
        }

        standing.At = at;

        return true;
    }
}
