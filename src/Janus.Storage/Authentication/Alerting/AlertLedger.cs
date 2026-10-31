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
/// Implements OPS-ALERT-002, OPS-ALERT-003 and CONV-DESIGN-003. A key is claimed by one
/// conditional statement (X3 of D-166, 290): a key never raised is inserted, and one
/// raised before is taken again only where its window has passed or the claim standing
/// is the same raise's and was never answered. A pass that overlaps another waits on the
/// row the other wrote and then finds it claimed, so two passes deliver one alert and a
/// new key faults neither.
/// </remarks>
internal sealed class AlertLedger(DataConnections connections) : IAlertLedger
{
    private const string Claim =
        """
        INSERT INTO identity.alerts (key, at, raise, unanswered) VALUES (@key, @at, @raise, true)
        ON CONFLICT (key) DO UPDATE SET at = excluded.at, raise = excluded.raise, unanswered = true
        WHERE identity.alerts.at <= @standing
           OR (identity.alerts.raise = @raise AND identity.alerts.unanswered);
        """;

    private const string Taken =
        """
        INSERT INTO identity.alerts (key, at, raise, unanswered) VALUES (@key, @at, @raise, false)
        ON CONFLICT (key) DO UPDATE SET at = excluded.at, raise = excluded.raise, unanswered = false
        WHERE identity.alerts.raise = @raise OR identity.alerts.unanswered;
        """;

    private const string Forget =
        """
        DELETE FROM identity.alerts WHERE key = @key AND raise = @raise AND unanswered;
        """;

    /// <inheritdoc/>
    public async ValueTask<bool> FirstAsync(
        string key,
        RaisedAlertId raise,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int claimed = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Claim,
                new
                {
                    key,
                    at = at.ToUniversalTime(),
                    raise = raise.Value,
                    standing = (at - window).ToUniversalTime(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return claimed > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TakenAsync(
        string key,
        RaisedAlertId raise,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int recorded = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Taken,
                new { key, at = at.ToUniversalTime(), raise = raise.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return recorded > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ForgetAsync(string key, RaisedAlertId raise, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int forgotten = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Forget,
                new { key, raise = raise.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return forgotten > 0;
    }
}
