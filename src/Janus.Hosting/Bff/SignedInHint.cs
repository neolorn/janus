using System;
using Janus.Authentication.Sessions;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The cookie on the deployment's parent domain saying only that someone on this browser
/// holds an auth session, which a sign-on that only checks reads to answer a guest at once.
/// </summary>
/// <param name="held">Which parent domain the start read the hint is kept on, where one is.</param>
/// <remarks>
/// Implements BFF-SESS-006 (D-279). The hint carries no identifier and grants nothing: its
/// value is <c>1</c>, and the session cookie stays on the authentication application's own
/// host. Where <c>session.hint.domain</c> named no domain at the start, no hint is written
/// and every check asks the authentication application; a change of the key takes effect
/// at the next start.
/// </remarks>
internal sealed class SignedInHint(SignedInHintDomain held)
{
    /// <summary>
    /// The cookie's name.
    /// </summary>
    public const string Cookie = "identity-signed-in";

    /// <summary>
    /// The parent domain the hint is kept on, or nothing where the deployment keeps none.
    /// </summary>
    public string? Domain => held.Domain;

    /// <summary>
    /// Whether the request carries the hint.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>Whether it does.</returns>
    public static bool Carried(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.ContainsKey(Cookie);
    }

    /// <summary>
    /// Writes the hint beside an auth session just created, lasting no longer than it.
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <param name="issued">The auth session just created.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public void Write(HttpResponse response, IssuedSession issued)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(issued);

        if (Domain is string domain)
        {
            CookieOptions options = Options(domain);

            options.Expires = issued.Lasts;
            response.Cookies.Append(Cookie, "1", options);
        }
    }

    /// <summary>
    /// Removes the hint, as a sign-out does, and as a check that finds no auth session
    /// does with one left behind.
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <exception cref="ArgumentNullException">The response is absent.</exception>
    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (Domain is string domain)
        {
            response.Cookies.Delete(Cookie, Options(domain));
        }
    }

    // BFF-SESS-006: secure, kept from script, sent on a top-level navigation, on
    // the parent domain the deployment names.
    private static CookieOptions Options(string domain) => new()
    {
        Domain = domain,
        Path = "/",
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
    };
}
