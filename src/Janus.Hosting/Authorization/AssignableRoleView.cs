using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One role the caller may give in an organization, as <c>GET /admin/grants/assignable</c>
/// answers it.
/// </summary>
/// <param name="Name">The role.</param>
/// <param name="GrantsMustEnd">Whether a grant of it is written only with an expiry.</param>
/// <remarks>Implements AUTHZ-GRANT-005 and chapter 09 section 8 (D-247).</remarks>
internal sealed record AssignableRoleView(string Name, bool GrantsMustEnd)
{
    /// <summary>
    /// The view of one role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The role is absent.</exception>
    public static AssignableRoleView Of(AssignableRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new AssignableRoleView(role.Name.ToString(), role.GrantsMustEnd);
    }
}
