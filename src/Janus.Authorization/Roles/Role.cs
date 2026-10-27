using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// A named bundle of permissions, held as data, with the roles its holders may hand
/// out. Adding a role and changing what it allows are rows, so neither needs a
/// deployment.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005 and chapter 10 section 3. A role's
/// permissions are read live wherever a grant naming it is evaluated, which is why
/// editing a role takes effect at once and bumps nobody's counter (AUTHZ-CACHE-001).
/// </remarks>
internal sealed class Role
{
    private readonly HashSet<Permission> _permissions;
    private readonly HashSet<RoleName> _handsOut;

    private Role(RoleName name, HashSet<Permission> permissions, HashSet<RoleName> handsOut)
    {
        Name = name;
        _permissions = permissions;
        _handsOut = handsOut;
    }

    /// <summary>
    /// What the role is called, which is what a grant names.
    /// </summary>
    public RoleName Name { get; }

    /// <summary>
    /// What it allows.
    /// </summary>
    public IReadOnlySet<Permission> Permissions => _permissions;

    /// <summary>
    /// The roles its holders may hand out (AUTHZ-GRANT-005, D-212).
    /// </summary>
    public IReadOnlySet<RoleName> HandsOut => _handsOut;

    /// <summary>
    /// A role allowing the permissions given.
    /// </summary>
    /// <param name="name">What it is called.</param>
    /// <param name="permissions">What it allows.</param>
    /// <param name="handsOut">The roles its holders may hand out, none where absent.</param>
    /// <returns>The role.</returns>
    /// <exception cref="ArgumentNullException">The permissions are absent.</exception>
    public static Role Of(
        RoleName name,
        IEnumerable<Permission> permissions,
        IEnumerable<RoleName>? handsOut = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return new Role(name, [.. permissions], [.. handsOut ?? []]);
    }

    /// <summary>
    /// Adds a permission to the role.
    /// </summary>
    /// <param name="permission">The permission.</param>
    public void Allow(Permission permission) => _permissions.Add(permission);

    /// <summary>
    /// Takes a permission out of the role.
    /// </summary>
    /// <param name="permission">The permission.</param>
    public void Disallow(Permission permission) => _permissions.Remove(permission);

    /// <summary>
    /// Whether the role allows a permission.
    /// </summary>
    /// <param name="permission">The permission in question.</param>
    /// <returns>Whether it allows it.</returns>
    public bool Allows(Permission permission) => _permissions.Contains(permission);
}
