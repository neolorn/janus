using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The accounts the single check would admit for one permission on one record, or across
/// one organization.
/// </summary>
/// <param name="Subjects">
/// Each account admitted: active, or restricted where the permission is reading, holding
/// the permission through a grant, a group, a container or a derivation that no deny
/// defeats.
/// </param>
/// <param name="Partial">
/// Whether evaluation stopped at <c>authz.reverselookup.budget</c>, so that the accounts
/// the derivations named in <paramref name="Unevaluated"/> would admit are missing.
/// </param>
/// <param name="Unevaluated">The derivations not evaluated, by the relationship each follows from.</param>
/// <remarks>
/// Implements AUTHZ-GATE-007 (D-218). A host that tells the people who may act on a
/// record asks this rather than keeping its own copy of who holds a role, which misses
/// denies and derived access.
/// </remarks>
public sealed record AdmittedAccounts(
    IReadOnlyList<SubjectId> Subjects,
    bool Partial,
    IReadOnlyList<string> Unevaluated);
