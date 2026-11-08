using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a host registers to refuse taking someone's access away under rules of its own:
/// the last holder of a role the business needs, say.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 and LIB-HOST-001 (D-281). The library calls it before it
/// revokes an allow grant, ends a membership (now, when an end is set in advance, when the
/// job <c>membership-ends</c> carries one out, and when a member brings their own end
/// forward) or takes a member out of a group that reaches an allow grant, on every path,
/// inside the change's transaction after its locks and after the admission, the step-up
/// and the reach. Removals are serialised per organization by a lock the library takes
/// before the call, so the check reads committed state. A grant's expiry, an erasure, a
/// deny's revocation, a record's removal and a derivation's writes are not checked. A
/// deployment registers at most one; one registered twice stops the start. None
/// registered, nothing is checked.
/// </remarks>
public interface IAccessRemovalCheck
{
    /// <summary>
    /// Judges one removal of access.
    /// </summary>
    /// <param name="removal">What is about to be removed, from whom, by whom and why.</param>
    /// <param name="cancellationToken">Abandons the change the removal belongs to.</param>
    /// <returns>
    /// Nothing where the removal may go ahead, or the refusal: a code of the host's own,
    /// following the format of every code and under no first segment the library's
    /// catalogue uses nor <c>session.</c>, with whatever details the host gives, which the
    /// answer carries. A check that throws refuses the removal as one that could not be
    /// checked.
    /// </returns>
    ValueTask<Result> CheckAsync(AccessRemoval removal, CancellationToken cancellationToken);
}
