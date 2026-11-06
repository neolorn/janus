using System;

namespace Janus.Core;

/// <summary>
/// A live grant the asking person's own account holds in its own name, as the subject
/// export carries it: never who wrote it or why, which are about the person who acted.
/// </summary>
/// <param name="Id">The grant's identifier.</param>
/// <param name="Kind">Whether somebody wrote it or a materialisation did.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="On">
/// The record it is on, or the type <c>organization</c> and the organization's
/// identifier where it is on the whole organization, as a grant is written.
/// </param>
/// <param name="Deny">Whether it takes access away rather than conferring it.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="GrantedAt">When it was written.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003 criterion 9 and PRIV-RIGHT-003 (D-274).
/// </remarks>
public sealed record OwnGrant(
    GrantId Id,
    GrantKind Kind,
    RoleName Role,
    ResourceReference On,
    bool Deny,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset GrantedAt);
