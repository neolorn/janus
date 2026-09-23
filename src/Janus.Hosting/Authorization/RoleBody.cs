using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What defining a role carries, as the request reads.
/// </summary>
/// <param name="Name">The role's name.</param>
/// <param name="Permissions">What holding it is to permit.</param>
/// <param name="Reason">Why, which every change to a role records.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-004.</remarks>
internal sealed record RoleBody(string? Name, IReadOnlyList<string?>? Permissions, string? Reason)
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

        return (new DefinedRole(name, permissions), string.Empty);
    }
}
