using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The subject set of each principal an operation evaluates for, resolved the first
/// time it is asked for and read from memory afterwards.
/// </summary>
/// <param name="groups">Where the transitive group set is read from.</param>
/// <param name="grants">Where the counter the set is keyed by is read from.</param>
/// <param name="restrictions">Where the account's processing restriction is read from.</param>
/// <param name="administrative">
/// Where the account's membership of the administrative organization is read from.
/// </param>
/// <remarks>
/// Implements AUTHZ-GROUP-002 and AUTHZ-CACHE-001. Ten checks in one request resolve
/// membership once. What is held is the group set and the counter it was read at, never
/// a resolved outcome: role definitions, ancestry, expiry and account state are read
/// live at every check. The instance lives for the operation, so nothing outlives the
/// transaction that could change it.
/// </remarks>
internal sealed class SubjectSets(
    IGroupStore groups,
    IGrantStore grants,
    ISubjectRestrictions restrictions,
    IAdministrativeOrganization administrative)
{
    private readonly Dictionary<SubjectId, SubjectSet> _resolved = [];

    /// <summary>
    /// Who the principal is, for the purpose of reading grants.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The set, which is empty for a principal holding no account and therefore confers
    /// nothing (AUTHZ-PRIN-003).
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<SubjectSet> OfAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return SubjectSet.None();
        }

        if (_resolved.TryGetValue(subject, out SubjectSet? held))
        {
            return held;
        }

        IReadOnlyList<GroupId> belongsTo = await groups
            .GroupsOfAsync(GrantSubject.Of(subject), cancellationToken)
            .ConfigureAwait(false);

        long version = await grants.VersionAsync(subject, cancellationToken).ConfigureAwait(false);

        bool restricted = await restrictions
            .IsRestrictedAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // IDN-LIFE-009a, D-166: a grant in the administrative organization confers only
        // on an account holding a current membership of it, read beside the group set.
        OrganizationId? withoutMembership = await administrative
            .WithoutMembershipAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var resolved = SubjectSet.Of(subject, belongsTo, version, restricted, withoutMembership);

        _resolved[subject] = resolved;

        return resolved;
    }

    /// <summary>
    /// Whether a modifying action of the principal is refused by a processing
    /// restriction.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the account is restricted, which a principal holding no account is not.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    /// <remarks>
    /// Implements AUTHZ-GATE-006 (D-183). Inside a transaction the state is read with
    /// the account's row held to that transaction's end, and never from the set resolved
    /// before it, so a restriction commits before the action, which is refused here, or
    /// after it. Outside one nothing can be held, and the resolved set answers.
    /// </remarks>
    public async ValueTask<bool> RestrictedAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return false;
        }

        return await restrictions.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
            ?? (await OfAsync(context, cancellationToken).ConfigureAwait(false)).Restricted;
    }
}
