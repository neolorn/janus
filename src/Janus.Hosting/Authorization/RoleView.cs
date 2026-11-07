using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One role as the management application reads it.
/// </summary>
/// <param name="Name">The role's name.</param>
/// <param name="Permissions">What holding it permits of itself.</param>
/// <param name="HandsOut">The roles its holders may hand out of itself.</param>
/// <param name="GrantsMustEnd">Whether every grant of it made from now on carries an end.</param>
/// <param name="Includes">Its inclusions of other roles, with their states and what they leave out.</param>
/// <param name="Effective">What it carries and hands out once its inclusions are followed.</param>
/// <remarks>
/// Implements chapter 09 section 8, AUTHZ-GRANT-004, AUTHZ-GRANT-005 and AUTHZ-GRANT-006
/// (D-237, D-278).
/// </remarks>
internal sealed record RoleView(
    string Name,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> HandsOut,
    bool GrantsMustEnd,
    IReadOnlyList<RoleInclusionView> Includes,
    EffectiveRoleView Effective)
{
    /// <summary>
    /// The view of one role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The role is absent.</exception>
    /// <remarks>
    /// A role read without what it carries through its inclusions carries its own alone.
    /// </remarks>
    public static RoleView Of(DefinedRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        EffectiveRole effective = role.Effective ?? new EffectiveRole(
            [.. role.Permissions.Select(permission => new EffectiveEntry<Permission>(permission, []))],
            [.. role.HandsOut.Select(handed => new EffectiveEntry<RoleName>(handed, []))]);

        return new RoleView(
            role.Name.ToString(),
            [.. role.Permissions.Select(permission => permission.ToString())],
            [.. role.HandsOut.Select(handed => handed.ToString())],
            role.GrantsMustEnd,
            [.. role.Includes.Select(RoleInclusionView.Of)],
            EffectiveRoleView.Of(effective));
    }
}
