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
/// Implements BFF-MACH-002 and CONV-DESIGN-003. The claim is one insert that does
/// nothing on conflict, so it holds against a concurrent delivery of the same event
/// without a read before it.
/// </remarks>
internal sealed class CallbackEventStore(StoreContext context, DataConnections connections)
    : ICallbackEvents
{
    private const string Claim =
        """
        INSERT INTO identity.callback_events (callback, identifier, claimed_at)
        VALUES (@callback, @identifier, @at)
        ON CONFLICT (callback, identifier) DO NOTHING;
        """;

    /// <inheritdoc/>
    public async ValueTask<bool> ClaimAsync(
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
                Claim,
                new { callback, identifier, at },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return claimed == 1;
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseAsync(
        string callback,
        byte[] identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        _ = await context.CallbackEvents
            .Where(claimed => claimed.Callback == callback && claimed.Identifier == identifier)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
