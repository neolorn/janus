using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where grants are read and written.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-GRANT-003, AUTHZ-CACHE-001 and CONV-DESIGN-003.
/// What a read returns is the rows a principal holds, never a resolved outcome: role
/// definitions, ancestry, expiry and account state are read live wherever the outcome
/// is worked out.
/// </remarks>
internal interface IGrantStore
{
    /// <summary>
    /// Reads one grant, revoked or not.
    /// </summary>
    /// <param name="id">Which grant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The grant, or nothing where no such row exists.</returns>
    ValueTask<Grant?> FindAsync(GrantId id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new grant, and bumps the counter of every account it reaches in the
    /// same transaction.
    /// </summary>
    /// <param name="grant">The grant to record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(Grant grant, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the grant as it now stands onto its row, and bumps the counter of every
    /// account it reaches in the same transaction.
    /// </summary>
    /// <param name="grant">The grant as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Grant grant, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a live grant saying exactly this already exists, which is what makes
    /// another one a duplicate.
    /// </summary>
    /// <param name="grant">The grant that would be written.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one already exists.</returns>
    ValueTask<bool> ExistsAsync(Grant grant, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any grant confers the role, live, expired or revoked, which is what keeps
    /// the role in place: a grant's history names it.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one does.</returns>
    ValueTask<bool> NamesAsync(RoleName role, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any grant was given to an account or a group, live, expired or revoked,
    /// which is what keeps a group in place: a grant's history names it.
    /// </summary>
    /// <param name="holder">The account or group.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one was.</returns>
    ValueTask<bool> NamesAsync(GrantSubject holder, CancellationToken cancellationToken);

    /// <summary>
    /// The live grants a principal holds, its own and those of every group it belongs
    /// to, within one organization.
    /// </summary>
    /// <param name="holders">The principal and the groups it belongs to.</param>
    /// <param name="organization">The organization the evaluation is scoped to.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The grants, revoked and expired ones left out.</returns>
    ValueTask<IReadOnlyList<Grant>> HeldByAsync(
        IReadOnlyList<GrantSubject> holders,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// The counter a principal's cached grant rows and group set are keyed by. It goes
    /// up in the same transaction as any grant or membership change reaching the
    /// account, which is what orphans the entry rather than expiring it.
    /// </summary>
    /// <param name="subject">Whose counter.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The counter, zero where nothing has ever changed for the account.</returns>
    ValueTask<long> VersionAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// The live grants a materialised derivation wrote for one role on a set of
    /// records, whoever holds them. Nothing is inherited here: a refresh reconciles
    /// the rows it wrote itself, and a grant on a container is not one of them.
    /// </summary>
    /// <param name="role">The role the derivation confers.</param>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="resources">The records the refresh is reconciling.</param>
    /// <param name="organization">The organization the records belong to.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The grants, revoked and expired ones left out.</returns>
    ValueTask<IReadOnlyList<Grant>> MaterialisedAsync(
        RoleName role,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// The live grants on one record, on anything containing it, or on the whole
    /// organization, whoever holds them. This is what the "who can access this?" view
    /// reads for stored grants.
    /// </summary>
    /// <param name="reference">The record in question.</param>
    /// <param name="organization">The organization the record belongs to.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The grants, nearest container first.</returns>
    ValueTask<IReadOnlyList<Grant>> OnAsync(
        ResourceReference reference,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
