using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A role the deployment starts with, as the host declares it: seeded once at a start
/// and from then on the administrators' to change.
/// </summary>
/// <param name="Name">
/// The role's name, as <c>POST /admin/roles</c> takes one and never a role chapter 10
/// section 3 seeds. It is kept as written, so a name outside the rule is refused at
/// startup naming the declaration.
/// </param>
/// <param name="Permissions">
/// What holding it permits: permissions the host declares, or the library's own other
/// than <c>system:administer</c>.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-004 and LIB-HOST-001 (D-204).</remarks>
public sealed record RoleDeclaration(string Name, IReadOnlyList<Permission> Permissions);
