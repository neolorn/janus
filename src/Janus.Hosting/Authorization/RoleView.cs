using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One role as the management application reads it.
/// </summary>
/// <param name="Name">The role's name.</param>
/// <param name="Permissions">What holding it permits.</param>
/// <param name="HandsOut">The roles its holders may hand out.</param>
/// <remarks>Implements chapter 09 section 8, AUTHZ-GRANT-004 and AUTHZ-GRANT-005.</remarks>
internal sealed record RoleView(string Name, IReadOnlyList<string> Permissions, IReadOnlyList<string> HandsOut)
{
    /// <summary>
    /// The view of one role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The role is absent.</exception>
    public static RoleView Of(DefinedRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleView(
            role.Name.ToString(),
            [.. role.Permissions.Select(permission => permission.ToString())],
            [.. role.HandsOut.Select(handed => handed.ToString())]);
    }
}
