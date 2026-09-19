using System;

namespace Janus.Core;

/// <summary>
/// The grant that decided an evaluation, as an explanation names it.
/// </summary>
/// <param name="Id">Which grant.</param>
/// <param name="Kind">Whether someone wrote it, a fact produced it, or it was precomputed.</param>
/// <param name="SubjectType">Whether it is held by an account or by a group.</param>
/// <param name="SubjectId">The account or the group holding it.</param>
/// <param name="Role">The role it confers.</param>
/// <param name="Deny">Whether it took the permission away rather than conferring it.</param>
/// <param name="InheritedFrom">
/// The container it sits on, where it reached the record through containment, and
/// nothing where it sits on the record itself or on the whole organization.
/// </param>
/// <remarks>Implements AUTHZ-GATE-004 and CONV-DESIGN-004.</remarks>
public sealed record ExplainedGrant(
    GrantId Id,
    GrantKind Kind,
    SubjectType SubjectType,
    Guid SubjectId,
    RoleName Role,
    bool Deny,
    ResourceReference? InheritedFrom);
