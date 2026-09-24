using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// When each actor's recent export operations were admitted, over the
/// <c>bulk_exports</c> table.
/// </summary>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements OPS-ALERT-006 and CONV-DESIGN-003. An actor is matched whether it is a
/// person or a system principal, so neither is ever counted against the other.
/// </remarks>
internal sealed class BulkExportLedger(DataConnections connections) : IBulkExportLedger
{
    private const string Since =
        """
        SELECT admitted_at FROM identity.bulk_exports
        WHERE actor IS NOT DISTINCT FROM @actor::uuid
          AND principal IS NOT DISTINCT FROM @principal::text
          AND admitted_at > @since
        ORDER BY admitted_at;
        """;

    private const string Record =
        """
        DELETE FROM identity.bulk_exports
        WHERE actor IS NOT DISTINCT FROM @actor::uuid
          AND principal IS NOT DISTINCT FROM @principal::text
          AND admitted_at <= @since;
        INSERT INTO identity.bulk_exports (id, actor, principal, admitted_at)
        VALUES (@id, @actor::uuid, @principal::text, @at);
        """;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // The provider reads a timestamptz as a DateTime in UTC.
        IEnumerable<DateTime> admitted = await ambient.Connection
            .QueryAsync<DateTime>(new CommandDefinition(
                Since,
                new { actor = actor?.Value, principal, since = since.ToUniversalTime() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return [.. admitted.Select(at => new DateTimeOffset(at, TimeSpan.Zero))];
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset at,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Record,
                new
                {
                    id = Guid.CreateVersion7(at),
                    actor = actor?.Value,
                    principal,
                    at = at.ToUniversalTime(),
                    since = since.ToUniversalTime(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
