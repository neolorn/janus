using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The library's own routes that non-browser callers reach.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-001. Which routes these are is settled here and by nothing a
/// deployment can set, so a browser endpoint cannot be moved onto the machine profile
/// by configuration and a machine endpoint cannot be left off it by omission.
/// </remarks>
internal static class MachineRoutes
{
    private static readonly PathString[] Governed =
    [
        new("/oidc/par"),
        new("/oidc/token"),
        new("/oidc/userinfo"),
        new("/callbacks/sms/dlr"),
    ];

    /// <summary>
    /// Whether the machine profile governs a path.
    /// </summary>
    /// <param name="path">The path the request arrived at.</param>
    /// <returns>Whether it is one of the library's machine routes.</returns>
    public static bool Governs(PathString path)
    {
        foreach (PathString governed in Governed)
        {
            if (path.Equals(governed, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
