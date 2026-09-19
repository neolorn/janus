using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a rendered rule is run against the library's own tables.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-002, AUTHZ-PRIN-001 and CONV-DESIGN-003. A single check is the
/// same rendering the host applies to its own query, read over the library's record of
/// the resources instead of the host's table, so that neither path can drift from the
/// other.
/// </remarks>
internal interface IAccessEvaluator
{
    /// <summary>
    /// The grants a rule matches on one record, the one that decides first.
    /// </summary>
    /// <param name="candidates">The rendered statement and its parameters.</param>
    /// <param name="resource">The record the statement is read for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The matched grants.</returns>
    ValueTask<IReadOnlyList<CandidateGrant>> CandidatesAsync(
        SqlFilter candidates,
        ResourceId resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// The grants a rule matches on the organization itself, the one that decides
    /// first.
    /// </summary>
    /// <param name="candidates">The rendered statement and its parameters.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The matched grants.</returns>
    ValueTask<IReadOnlyList<CandidateGrant>> OrganizationCandidatesAsync(
        SqlFilter candidates,
        CancellationToken cancellationToken);

    /// <summary>
    /// What each record of a page confers, read in one query for the whole page.
    /// </summary>
    /// <param name="page">The rendered statement and its parameters.</param>
    /// <param name="resources">The records of the page.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>One row per record and permission a grant named.</returns>
    ValueTask<IReadOnlyList<PageCapability>> PageAsync(
        SqlFilter page,
        IReadOnlyList<ResourceId> resources,
        CancellationToken cancellationToken);
}
