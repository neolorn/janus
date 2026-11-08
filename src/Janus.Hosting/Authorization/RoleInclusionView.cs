using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One of a role's inclusions of another as the management application reads it.
/// </summary>
/// <param name="Role">The role included.</param>
/// <param name="Enabled">Whether the inclusion admits.</param>
/// <param name="LeftOut">What it does not pass on.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record RoleInclusionView(string Role, bool Enabled, LeftOutView LeftOut)
{
    /// <summary>
    /// The view of one inclusion.
    /// </summary>
    /// <param name="inclusion">The inclusion.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The inclusion is absent.</exception>
    public static RoleInclusionView Of(RoleInclusion inclusion)
    {
        ArgumentNullException.ThrowIfNull(inclusion);

        return new RoleInclusionView(
            inclusion.Included.ToString(),
            inclusion.Enabled,
            new LeftOutView(
                [.. inclusion.LeftOutPermissions.Select(permission => permission.ToString())],
                [.. inclusion.LeftOutHandsOut.Select(handed => handed.ToString())]));
    }
}
