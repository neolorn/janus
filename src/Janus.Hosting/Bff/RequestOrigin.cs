using System;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// Where a request came from, in the terms a session records.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-013 and INT-GEN-006. The address is the one
/// the connection arrived on, never one a header claims: a deployment behind a proxy
/// tells the framework which proxies it trusts, and nothing here second-guesses that.
/// The description is the two coarse facts a person recognises their own device by
/// and no more; anything finer would be a fingerprint, which is not what the list is
/// for. The location stays absent until a local database can resolve one, which
/// INT-GEN-006 allows and which is why the field is optional.
/// </remarks>
internal static class RequestOrigin
{
    private const string Unknown = "unknown";

    /// <summary>
    /// Reads where one request came from.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The origin.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static SessionOrigin Of(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string agent = request.Headers.UserAgent.ToString();

        return new SessionOrigin(
            Source(request),
            new DeviceDescription(Browser(agent), System(agent)),
            Location: null);
    }

    /// <summary>
    /// Where one request came from, for the counters that are kept per source
    /// (AUTH-ABUSE-001, AUTH-ABUSE-008).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static string Source(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? Unknown;
    }

    // Order matters: the engines that name themselves after the ones they replaced
    // come first, so a browser is called what its user calls it.
    private static string Browser(string agent) =>
        Named(agent, "Edg/", "Edge")
        ?? Named(agent, "OPR/", "Opera")
        ?? Named(agent, "Firefox/", "Firefox")
        ?? Named(agent, "Chrome/", "Chrome")
        ?? Named(agent, "Safari/", "Safari")
        ?? Unknown;

    private static string System(string agent) =>
        Named(agent, "Windows", "Windows")
        ?? Named(agent, "Android", "Android")
        ?? Named(agent, "iPhone", "iOS")
        ?? Named(agent, "iPad", "iPadOS")
        ?? Named(agent, "Mac OS X", "macOS")
        ?? Named(agent, "Linux", "Linux")
        ?? Unknown;

    private static string? Named(string agent, string token, string name) =>
        agent.Contains(token, StringComparison.Ordinal) ? name : null;
}
