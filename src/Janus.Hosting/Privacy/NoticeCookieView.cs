using System;
using System.Xml;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One cookie a version of the privacy notice lists, as chapter 09 gives it.
/// </summary>
/// <param name="Name">What the cookie is called.</param>
/// <param name="Purpose">The consent-based purpose it serves, or null where it is strictly necessary.</param>
/// <param name="Lifetime">
/// How long it is set for as an ISO 8601 duration, or null where it ends with the
/// browser's session.
/// </param>
/// <param name="ThirdParty">Whether any third party can read it.</param>
/// <remarks>Implements PRIV-CONS-006a and chapter 09 <c>GET /privacy/notice</c> (D-203).</remarks>
internal sealed record NoticeCookieView(string Name, string? Purpose, string? Lifetime, bool ThirdParty)
{
    /// <summary>
    /// The view of a listed cookie.
    /// </summary>
    /// <param name="cookie">The cookie.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The cookie is absent.</exception>
    public static NoticeCookieView Of(NoticeCookie cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);

        return new NoticeCookieView(
            cookie.Name,
            cookie.Purpose,
            cookie.Lifetime is TimeSpan lifetime ? XmlConvert.ToString(lifetime) : null,
            cookie.ThirdParty);
    }
}
