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
/// <remarks>
/// Implements AUTHZ-GROUP-002 and AUTHZ-CACHE-001. Ten checks in one request resolve
/// membership once. What is held is the group set and the counter it was read at, never
/// a resolved outcome: role definitions, ancestry, expiry and account state are read
/// live at every check. The instance lives for the operation, so nothing outlives the
/// transaction that could change it.
/// </remarks>
internal sealed class SubjectSets(IGroupStore groups, IGrantStore grants)
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
        var resolved = SubjectSet.Of(subject, belongsTo, version);

        _resolved[subject] = resolved;

        return resolved;
    }
}
