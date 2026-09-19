using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one place a permission is evaluated. A check and a list filter are the same
/// rule asked two ways, so a list screen cannot come to show what a check would
/// refuse.
/// </summary>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-PRIN-001, AUTHZ-PRIN-002, AUTHZ-GATE-001,
/// AUTHZ-GATE-002, AUTHZ-GATE-004, AUTHZ-GATE-005 and LIB-SEAM-001. Every evaluation
/// is scoped to the organization owning the record, resolved from the record and never
/// from a session (AUTHZ-SCOPE-001), and every path fails closed (AUTHZ-PRIN-003).
/// </remarks>
public interface IAccessGate
{
    /// <summary>
    /// Whether the caller may do this to this record.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or <c>authz.denied</c> carrying the correlation identifier the audit
    /// trail records it under. What the caller answers with is the type's declared
    /// concealment behaviour: a concealing type answers as a record that does not
    /// exist, a disclosing one says the record exists and is forbidden.
    /// </returns>
    ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same rule as a predicate over the host's own rows, for the host to apply to
    /// its own query.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="type">The kind of thing the rows are.</param>
    /// <param name="organization">The organization the listing is within.</param>
    /// <param name="sources">The contract tables and the identifier selector.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The predicate, which enumerates nothing and returns no record.</returns>
    ValueTask<Result<Expression<Func<TResource, bool>>>> FilterAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same rule as a fragment a hand-written query composes into its <c>WHERE</c>
    /// clause.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="type">The kind of thing the rows are.</param>
    /// <param name="organization">The organization the listing is within.</param>
    /// <param name="rowAlias">The alias the query gives the row.</param>
    /// <param name="column">The column of that row holding the record's identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The fragment and its parameters.</returns>
    /// <exception cref="ArgumentException">
    /// The row alias or the column is not an identifier, which is what would let a value
    /// into the fragment's text.
    /// </exception>
    ValueTask<Result<SqlFilter>> FragmentAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        string rowAlias,
        string column,
        CancellationToken cancellationToken);

    /// <summary>
    /// Why access was granted or refused, naming the grant that decided or stating
    /// that none matched.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The explanation, or <c>authz.denied</c> where the type conceals its records and
    /// the caller is asking about their own denial: an explanation saying that no grant
    /// matched would disclose that the record exists (AUTHZ-GATE-004).
    /// </returns>
    ValueTask<Result<AccessExplanation>> ExplainAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the caller may do to each of these records, computed for the whole page at
    /// once rather than a query per row.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="resources">The records of the page.</param>
    /// <param name="permissions">The permissions the caller's surface offers on them.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>One capability per record, in the order the records were given.</returns>
    ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken);
}
