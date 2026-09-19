using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authorization.Model;

/// <summary>
/// What the database's own catalogue says a relation can be looked up by.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-004 and CONV-DESIGN-003. A derived grant is only as fast as
/// the join it performs into the host's relation, so the columns a derivation names are
/// checked against this at startup.
/// </remarks>
internal interface IIndexCatalogue
{
    /// <summary>
    /// The columns of one relation that an index reaches its rows by, which is the
    /// leading column of each index over it.
    /// </summary>
    /// <param name="relation">The relation, as SQL names it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The columns, which is nothing where no such relation exists.</returns>
    ValueTask<IReadOnlySet<string>> IndexedAsync(string relation, CancellationToken cancellationToken);
}
