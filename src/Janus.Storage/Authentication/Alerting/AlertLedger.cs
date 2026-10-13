using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Alerting;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// Where the conditions already raised are remembered.
/// </summary>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements OPS-ALERT-002 and CONV-DESIGN-003. A key is claimed by one conditional
/// statement (X3 of D-166, 290): a key never raised is inserted, and one raised before
/// is taken again only where its window has passed. A pass that overlaps another waits
/// on the row the other wrote and then finds it claimed, so two passes deliver one
/// alert and a new key faults neither.
/// </remarks>
internal sealed class AlertLedger(DataConnections connections) : IAlertLedger
{
    private const string Claim =
        """
        INSERT INTO identity.alerts (key, at) VALUES (@key, @at)
        ON CONFLICT (key) DO UPDATE SET at = excluded.at
        WHERE identity.alerts.at <= @standing;
        """;

    /// <inheritdoc/>
    public async ValueTask<bool> FirstAsync(
        string key,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int claimed = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Claim,
                new { key, at = at.ToUniversalTime(), standing = (at - window).ToUniversalTime() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return claimed > 0;
    }
}
