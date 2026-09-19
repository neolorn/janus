using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Model;

namespace Janus.Storage.Authorization.Model;

/// <summary>
/// What a relation can be looked up by, read from the database's own catalogue.
/// </summary>
/// <param name="connections">Where the statement takes its connection from.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-004 and CONV-DESIGN-003. The leading column of an index is
/// what a lookup by that column reaches the rows by; a column further along one is not,
/// and an index the database has not finished building reaches nothing.
/// </remarks>
internal sealed class IndexCatalogue(DataConnections connections) : IIndexCatalogue
{
    private const string Columns =
        """
        SELECT DISTINCT attribute.attname
        FROM pg_index AS indexed
        JOIN pg_class AS held ON held.oid = indexed.indrelid
        JOIN pg_namespace AS containing ON containing.oid = held.relnamespace
        JOIN pg_attribute AS attribute
          ON attribute.attrelid = held.oid AND attribute.attnum = indexed.indkey[0]
        WHERE containing.nspname = @schema
          AND held.relname = @relation
          AND indexed.indisvalid;
        """;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<string>> IndexedAsync(
        string relation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relation);

        string[] named = relation.Split('.');

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<string> columns = await ambient.Connection
            .QueryAsync<string>(new CommandDefinition(
                Columns,
                new
                {
                    schema = named.Length > 1 ? named[0] : "public",
                    relation = named[^1],
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return columns.ToFrozenSet(StringComparer.Ordinal);
    }
}
