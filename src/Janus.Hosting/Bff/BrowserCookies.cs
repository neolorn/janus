using System;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What the browser carries, and under what attributes.
/// </summary>
/// <remarks>
/// Implements BFF-SESS-002, BFF-CSRF-003, BFF-CSRF-005 and BFF-CSRF-006. The
/// attributes are not configurable: the names and the four attributes are fixed here
/// so that no deployment can arrive at an insecure one by leaving a key unset.
/// </remarks>
internal static class BrowserCookies
{
    /// <summary>
    /// The session cookie, which the browser cannot read (D-153).
    /// </summary>
    public const string Session = "__Host-janus-session";

    /// <summary>
    /// The cookie a browser carries before it holds a session, which is what a
    /// synchronizer token binds to until one exists (D-153, BFF-CSRF-005a).
    /// </summary>
    public const string PreAuthentication = "__Host-janus-preauth";

    /// <summary>
    /// The synchronizer token cookie, which the first-party frontend reads on load so
    /// that obtaining the token costs no round trip (D-153).
    /// </summary>
    public const string Csrf = "__Host-janus-csrf";

    /// <summary>
    /// What a browser the account has been seen from carries, so that its next
    /// sign-in is not held for a code (D-153, AUTH-FACT-016).
    /// </summary>
    public const string Browser = "__Host-janus-browser";

    /// <summary>
    /// The header a state-changing request carries, whose presence is checked and
    /// whose value is ignored (D-153).
    /// </summary>
    public const string RequestHeader = "X-Janus-Request";

    /// <summary>
    /// The attributes every cookie the library sets carries.
    /// </summary>
    /// <param name="application">Which application the pipeline is mounted in.</param>
    /// <param name="readableByScript">
    /// Whether the first-party frontend reads the value, which only the synchronizer
    /// token does.
    /// </param>
    /// <returns>The attributes.</returns>
    public static CookieOptions Options(JanusApplication application, bool readableByScript) => new()
    {
        HttpOnly = !readableByScript,

        // The three the __Host- prefix requires: secure, rooted, and no domain, which
        // is what keeps one application's cookie out of another subdomain's reach.
        Secure = true,
        Path = "/",

        SameSite = application is JanusApplication.Management
            ? SameSiteMode.Strict
            : SameSiteMode.Lax,

        // The session and its token are strictly necessary, so a cookie policy that
        // withholds what consent has not covered does not withhold these.
        IsEssential = true,
    };

    /// <summary>
    /// The same attributes, on a cookie that outlives the browser being closed
    /// because what it stands for outlives it.
    /// </summary>
    /// <param name="application">Which application the pipeline is mounted in.</param>
    /// <param name="until">When the browser is to forget it.</param>
    /// <returns>The attributes.</returns>
    public static CookieOptions Lasting(JanusApplication application, DateTimeOffset until)
    {
        CookieOptions options = Options(application, readableByScript: false);

        options.Expires = until;

        return options;
    }
}
