using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A declared role's inclusion of another role, as the host declares it: seeded once at
/// a start and from then on the administrators' to change.
/// </summary>
/// <param name="Role">
/// The role included: one the host declares or one chapter 10 section 3 seeds, kept as
/// written so a name outside them is refused at startup naming the including role.
/// </param>
/// <param name="LeftOutPermissions">
/// The permissions the inclusion does not pass on, each a permission the library or the
/// host declares, kept as written; none where empty.
/// </param>
/// <param name="LeftOutHandsOut">
/// The roles of the included role's <c>handsOut</c> the inclusion does not pass on, kept
/// as written; none where empty.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-006 and LIB-HOST-001 (D-278).</remarks>
public sealed record RoleInclusionDeclaration(
    string Role,
    IReadOnlyList<string> LeftOutPermissions,
    IReadOnlyList<string> LeftOutHandsOut);
