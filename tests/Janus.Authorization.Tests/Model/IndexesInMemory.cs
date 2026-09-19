using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// What the deployment's relations can be looked up by, held in memory.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from what it holds, so a test that indexes a
/// column sees what the startup check would see.
/// </remarks>
internal sealed class IndexesInMemory : IIndexCatalogue
{
    private readonly Dictionary<string, HashSet<string>> _indexed = new(StringComparer.Ordinal);

    /// <summary>
    /// Indexes a column of a relation.
    /// </summary>
    /// <param name="relation">The relation, as SQL names it.</param>
    /// <param name="column">The column an index reaches its rows by.</param>
    public void Index(string relation, string column)
    {
        if (!_indexed.TryGetValue(relation, out HashSet<string>? columns))
        {
            columns = new HashSet<string>(StringComparer.Ordinal);
            _indexed.Add(relation, columns);
        }

        columns.Add(column);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlySet<string>> IndexedAsync(
        string relation,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlySet<string>>(
            _indexed.TryGetValue(relation, out HashSet<string>? columns)
                ? columns
                : new HashSet<string>(StringComparer.Ordinal));
}
