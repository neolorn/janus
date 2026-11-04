using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Search;
using Janus.Core;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// Each account's staff searches a minute, over the <c>search_windows</c> table.
/// </summary>
/// <param name="connections">Where the statement takes its connection from.</param>
/// <remarks>
/// Implements IDN-SRCH-002 and CONV-DESIGN-003 (D-256). The count is one upsert, so two
/// searches at once are counted one after the other by the row's own lock, and the same
/// statement forgets the caller's earlier minutes.
/// </remarks>
internal sealed class SearchWindows(DataConnections connections) : ISearchWindows
{
    private const string Count =
        """
        WITH forgotten AS (
            DELETE FROM identity.search_windows
            WHERE subject = @subject AND window_start < @window
        )
        INSERT INTO identity.search_windows (subject, window_start, count)
        VALUES (@subject, @window, 1)
        ON CONFLICT (subject, window_start)
        DO UPDATE SET count = identity.search_windows.count + 1
        RETURNING count;
        """;

    /// <inheritdoc/>
    public async ValueTask<int> CountAsync(SubjectId subject, DateTimeOffset window, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(
                Count,
                new { subject = subject.Value, window },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
