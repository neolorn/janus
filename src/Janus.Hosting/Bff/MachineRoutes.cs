using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The library's own routes that non-browser callers reach.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-001, IDN-LIFE-012a AC3, REG-IDENT-008 and OPS-BOOT-002. Which routes these are
/// is settled here and by nothing a deployment can set, so a browser endpoint cannot be
/// moved onto the machine profile by configuration and a machine endpoint cannot be
/// left off it by omission.
/// </remarks>
internal static class MachineRoutes
{
    private static readonly PathString[] Governed =
    [
        new("/auth/break-glass"),
        new("/oidc/par"),
        new("/oidc/token"),
        new("/oidc/userinfo"),
        new("/callbacks/sms/dlr"),
        new("/callbacks/providers/google"),
        new("/callbacks/providers/apple"),
        new("/callbacks/providers/google/return"),
        new("/callbacks/providers/apple/return"),
    ];

    // Chapter 09 section 8: the emergency credential is presented from a browser that
    // may still hold a stale session for the domain, so the cookie is ignored there
    // rather than refused. A provider returns the browser itself, which carries its
    // lax cookie on a returning read; the return reads no cookie and only sends the
    // browser on to the browser profile, which judges it.
    private static readonly PathString[] CookieIgnored =
    [
        new("/auth/break-glass"),
        new("/callbacks/providers/google/return"),
        new("/callbacks/providers/apple/return"),
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

    /// <summary>
    /// Whether a route on the machine profile ignores a session cookie rather than
    /// refusing the request that carries one.
    /// </summary>
    /// <param name="path">The path the request arrived at.</param>
    /// <returns>Whether the cookie is ignored there.</returns>
    public static bool IgnoresCookie(PathString path)
    {
        foreach (PathString ignoring in CookieIgnored)
        {
            if (path.Equals(ignoring, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
