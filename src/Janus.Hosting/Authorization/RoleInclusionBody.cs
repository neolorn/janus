using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What making or setting a role's inclusion of another carries, as the request reads.
/// </summary>
/// <param name="Enabled">Whether the inclusion admits, or nothing for on.</param>
/// <param name="LeftOut">What it does not pass on, or nothing for nothing.</param>
/// <param name="Reason">Why, which every change to an inclusion records.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record RoleInclusionBody(bool? Enabled, LeftOutBody? LeftOut, string? Reason)
{
    /// <summary>
    /// The inclusion the body describes, or nothing where what it leaves out does not
    /// read, which is the member <c>leftOut</c>.
    /// </summary>
    /// <param name="included">The role included, which the path names.</param>
    /// <returns>The inclusion, or nothing.</returns>
    public RoleInclusion? Read(RoleName included)
    {
        var permissions = new List<Permission>(LeftOut?.Permissions?.Count ?? 0);

        foreach (string? written in LeftOut?.Permissions ?? [])
        {
            if (!Permission.TryParse(written, out Permission permission))
            {
                return null;
            }

            permissions.Add(permission);
        }

        var handsOut = new List<RoleName>(LeftOut?.HandsOut?.Count ?? 0);

        foreach (string? written in LeftOut?.HandsOut ?? [])
        {
            if (!RoleName.TryParse(written, out RoleName handed))
            {
                return null;
            }

            handsOut.Add(handed);
        }

        // 09 (D-278): enabled is optional and on where absent; leftOut is optional and
        // empty where absent.
        return new RoleInclusion(included, Enabled ?? true, permissions, handsOut);
    }
}
