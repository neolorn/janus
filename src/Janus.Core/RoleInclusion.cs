using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A role's inclusion of another: whether it is on, and what it does not pass on.
/// </summary>
/// <param name="Included">The role included.</param>
/// <param name="Enabled">Whether the inclusion admits; off, it is kept and passes nothing on.</param>
/// <param name="LeftOutPermissions">The permissions it does not pass on, none where empty.</param>
/// <param name="LeftOutHandsOut">
/// The roles of the included role's <c>handsOut</c> it does not pass on, none where empty.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-006 and chapter 09 section 8 (D-278).</remarks>
public sealed record RoleInclusion(
    RoleName Included,
    bool Enabled,
    IReadOnlyList<Permission> LeftOutPermissions,
    IReadOnlyList<RoleName> LeftOutHandsOut);
