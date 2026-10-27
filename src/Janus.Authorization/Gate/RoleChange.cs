using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// A change to a role, judged against the reach of the actor making it.
/// </summary>
/// <param name="Before">The role as it stood, or nothing where the change defines it.</param>
/// <param name="After">The role as the change leaves it, or nothing where the change removes it.</param>
/// <param name="HandedOut">The roles the role hands out after the change, each as the deployment defines it.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-212). Nobody widens a role, or the roles its holders hand
/// out, beyond what the actor may give or hand out itself.
/// </remarks>
internal sealed record RoleChange(DefinedRole? Before, DefinedRole? After, IReadOnlyList<DefinedRole> HandedOut);
