using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where a materialised derivation is brought back into step with the host's own
/// relation.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-005 (D-161). The host calls this from the operation that
/// changes the relationship, inside the same unit of work, so the grants and the fact
/// they were computed from are written together or not at all. The same call is what
/// the drift check runs, and what it changes there is drift.
/// </remarks>
public interface IDerivationMaterialiser
{
    /// <summary>
    /// Writes the grants the relationship's rows confer on one record, and takes back
    /// the ones they no longer confer.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking, recorded on every grant the refresh writes.</param>
    /// <param name="derivation">The relationship the materialised derivations follow from.</param>
    /// <param name="resource">The record the relationship's rows are about.</param>
    /// <param name="sources">The rows of that relationship, from the host's own context.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the refresh changed.</returns>
    /// <exception cref="System.ArgumentNullException">The context or the sources are absent.</exception>
    /// <exception cref="System.ArgumentException">
    /// The relationship is undeclared, no materialised derivation follows from it, the
    /// rows of it were not supplied, or the context names no subject to record the
    /// grants against.
    /// </exception>
    ValueTask<Result<DerivationRefresh>> RefreshAsync<TResource>(
        AccessContext context,
        string derivation,
        ResourceId resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);
}
