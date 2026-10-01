using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Callbacks;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// Where the provider events a callback was carried for are claimed.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements BFF-MACH-002, CONV-DESIGN-003 and entry 276. A claim is one insert, which
/// holds against a concurrent delivery of the same event without a read before it. A
/// host callback's insert takes over, where it conflicts, a claim left unsettled past
/// the timeout, and the row it conflicts with is locked whether or not it is taken
/// over, so what the delivery is then told of the claim is what stands (D-166 X3). A
/// claim is settled and given back only by the delivery that took it, named by the
/// instant it took it at, so a delivery overtaken by another cannot settle or give back
/// the other's claim.
/// </remarks>
internal sealed class CallbackEventStore(StoreContext context, DataConnections connections)
    : ICallbackEvents
{
    private const string Claim =
        """
        INSERT INTO identity.callback_events (callback, identifier, claimed_at, settled_at)
        VALUES (@callback, @identifier, @at, NULL)
        ON CONFLICT (callback, identifier) DO UPDATE SET claimed_at = EXCLUDED.claimed_at
        WHERE callback_events.settled_at IS NULL AND callback_events.claimed_at <= @stale;
        """;

    private const string Standing =
        """
        SELECT settled_at IS NOT NULL
        FROM identity.callback_events
        WHERE callback = @callback AND identifier = @identifier;
        """;

    private const string Carry =
        """
        INSERT INTO identity.callback_events (callback, identifier, claimed_at, settled_at)
        VALUES (@callback, @identifier, @at, @at)
        ON CONFLICT (callback, identifier) DO NOTHING;
        """;

    /// <inheritdoc/>
    public async ValueTask<CallbackClaim> ClaimAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        DateTimeOffset stale,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int taken = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Claim,
                new { callback, identifier, at, stale },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (taken == 1)
        {
            return CallbackClaim.Taken;
        }

        bool settled = await ambient.Connection
            .QuerySingleAsync<bool>(new CommandDefinition(
                Standing,
                new { callback, identifier },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return settled ? CallbackClaim.Settled : CallbackClaim.InProgress;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CarryAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int claimed = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Carry,
                new { callback, identifier, at },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return claimed == 1;
    }

    /// <inheritdoc/>
    public async ValueTask SettleAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        _ = await Held(callback, identifier, claimed)
            .ExecuteUpdateAsync(
                row => row.SetProperty(held => held.SettledAt, at),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        _ = await Held(callback, identifier, claimed)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // The unsettled claim the delivery that took it at that instant still holds.
    private IQueryable<CallbackEventRecord> Held(string callback, byte[] identifier, DateTimeOffset claimed) =>
        context.CallbackEvents.Where(held =>
            held.Callback == callback
            && held.Identifier == identifier
            && held.ClaimedAt == claimed
            && held.SettledAt == null);
}
