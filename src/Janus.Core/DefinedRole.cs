using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A role as the deployment holds it: a name, the permissions it bundles and the roles
/// its holders may hand out.
/// </summary>
/// <param name="Name">The role's name, which grants carry.</param>
/// <param name="Permissions">What holding it permits.</param>
/// <param name="HandsOut">
/// The roles a holder may hand out where the role is held (AUTHZ-GRANT-005), none where
/// empty.
/// </param>
/// <param name="GrantsMustEnd">
/// Whether every grant of it made from now on carries an end (AUTHZ-GRANT-003, D-237).
/// </param>
/// <remarks>Implements AUTHZ-GRANT-001, AUTHZ-GRANT-004, AUTHZ-GRANT-005 and chapter 09 section 8.</remarks>
public sealed record DefinedRole(
    RoleName Name,
    IReadOnlyList<Permission> Permissions,
    IReadOnlyList<RoleName> HandsOut,
    bool GrantsMustEnd = false)
{
    /// <summary>
    /// The role's inclusions of other roles, by the included role's name, none where empty.
    /// A definition leaves them as they stand (AUTHZ-GRANT-006, D-278).
    /// </summary>
    public IReadOnlyList<RoleInclusion> Includes { get; init; } = [];

    /// <summary>
    /// What the role carries and hands out once its inclusions are followed, or nothing
    /// where the role was not read from the deployment. A definition ignores it
    /// (AUTHZ-GRANT-006, D-278).
    /// </summary>
    public EffectiveRole? Effective { get; init; }
}
