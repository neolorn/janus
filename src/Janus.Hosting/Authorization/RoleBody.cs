using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What defining a role carries, as the request reads.
/// </summary>
/// <param name="Name">The role's name.</param>
/// <param name="Permissions">What holding it is to permit.</param>
/// <param name="HandsOut">The roles its holders may hand out, or nothing for none.</param>
/// <param name="GrantsMustEnd">
/// Whether every grant of it made from now on carries an end, or nothing for not.
/// </param>
/// <param name="Reason">Why, which every change to a role records.</param>
/// <remarks>Implements chapter 09 section 8, AUTHZ-GRANT-004 and AUTHZ-GRANT-005 (D-237).</remarks>
internal sealed record RoleBody(
    string? Name,
    IReadOnlyList<string?>? Permissions,
    IReadOnlyList<string?>? HandsOut,
    bool? GrantsMustEnd,
    string? Reason)
{
    /// <summary>
    /// The role the body describes, or the member it cannot be read at.
    /// </summary>
    /// <returns>The role, or nothing and the member that stopped it.</returns>
    public (DefinedRole? Role, string Member) Read()
    {
        if (!RoleName.TryParse(Name, out RoleName name))
        {
            return (null, "name");
        }

        if (Permissions is null)
        {
            return (null, "permissions");
        }

        var permissions = new List<Permission>(Permissions.Count);

        foreach (string? written in Permissions)
        {
            if (!Permission.TryParse(written, out Permission permission))
            {
                return (null, "permissions");
            }

            permissions.Add(permission);
        }

        // D-212: the list is optional, and a role whose body names none hands out none.
        var handsOut = new List<RoleName>(HandsOut?.Count ?? 0);

        foreach (string? written in HandsOut ?? [])
        {
            if (!RoleName.TryParse(written, out RoleName handed))
            {
                return (null, "handsOut");
            }

            handsOut.Add(handed);
        }

        // D-237: the member is optional, and a role whose body names none binds no grant.
        return (new DefinedRole(name, permissions, handsOut, GrantsMustEnd ?? false), string.Empty);
    }
}
