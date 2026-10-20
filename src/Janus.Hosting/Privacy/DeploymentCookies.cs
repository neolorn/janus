using System;
using System.Collections.Frozen;
using Janus.Hosting.Bff;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The cookies a deployment sets: the library's own and those the host declares.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-006a and BFF-SESS-002 (D-203). The library sets no cookie but
/// the five BFF-SESS-002 names, so a host cookie under one of those names would be
/// listed twice and set over the library's.
/// </remarks>
internal static class DeploymentCookies
{
    /// <summary>
    /// The names of the cookies the library sets.
    /// </summary>
    public static FrozenSet<string> Library { get; } = new[]
    {
        BrowserCookies.Session,
        BrowserCookies.PreAuthentication,
        BrowserCookies.Csrf,
        BrowserCookies.Device,
        BrowserCookies.Browser,
    }.ToFrozenSet(StringComparer.Ordinal);
}
