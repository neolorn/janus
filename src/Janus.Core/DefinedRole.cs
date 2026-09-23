using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A role as the deployment holds it: a name and the permissions it bundles.
/// </summary>
/// <param name="Name">The role's name, which grants carry.</param>
/// <param name="Permissions">What holding it permits.</param>
/// <remarks>Implements AUTHZ-GRANT-001, AUTHZ-GRANT-004 and chapter 09 section 8.</remarks>
public sealed record DefinedRole(RoleName Name, IReadOnlyList<Permission> Permissions);
