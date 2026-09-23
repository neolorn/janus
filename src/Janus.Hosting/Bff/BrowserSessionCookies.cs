using System;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The two cookies a browser carries for a session, written together and cleared
/// together.
/// </summary>
/// <param name="application">Which application this process serves.</param>
/// <remarks>
/// Implements BFF-SESS-001, BFF-SESS-002, BFF-SESS-004, BFF-SESS-005, BFF-CSRF-005
/// and BFF-CSRF-006. The browser receives two opaque values and nothing else: no
/// token, claim, permission or role name crosses in a cookie, and neither value is
/// readable from the record, which holds only what each fingerprints to. Writing the
/// pair again is what rotation looks like at the boundary, and the previous pair
/// stops working because the record stopped answering to it.
/// </remarks>
internal sealed class BrowserSessionCookies(ApplicationKind application)
{
    /// <summary>
    /// Writes the pair a newly issued session answers to, replacing whatever the
    /// browser carried before.
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <param name="issued">The session just issued.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public void Write(HttpResponse response, IssuedSession issued)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(issued);

        response.Cookies.Append(
            BrowserCookies.Session,
            issued.Secret.Value,
            BrowserCookies.Options(application, readableByScript: false));

        response.Cookies.Append(
            BrowserCookies.Csrf,
            issued.CsrfToken.Value,
            BrowserCookies.Options(application, readableByScript: true));
    }

    /// <summary>
    /// Writes the pair a browser carries before it holds a session (BFF-CSRF-005a).
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <param name="issued">The first contact just issued.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public void Write(HttpResponse response, IssuedPreAuthentication issued)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(issued);

        response.Cookies.Append(
            BrowserCookies.PreAuthentication,
            issued.Secret.Value,
            BrowserCookies.Options(application, readableByScript: false));

        response.Cookies.Append(
            BrowserCookies.Csrf,
            issued.CsrfToken.Value,
            BrowserCookies.Options(application, readableByScript: true));
    }

    /// <summary>
    /// Clears what a browser carried before it held a session, which is what
    /// authentication does rather than leaving it beside the session it became
    /// (BFF-CSRF-005a AC3).
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <exception cref="ArgumentNullException">The response is absent.</exception>
    public void ClearFirstContact(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(
            BrowserCookies.PreAuthentication,
            BrowserCookies.Options(application, readableByScript: false));
    }

    /// <summary>
    /// Writes what a browser the account has been seen from carries, so the next
    /// sign-in from it is not held for a code (AUTH-FACT-016, REG-SESS-007).
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <param name="token">The token the record answers to.</param>
    /// <param name="until">When the browser is to forget it.</param>
    /// <exception cref="ArgumentNullException">The response is absent.</exception>
    public void Remembered(HttpResponse response, OpaqueToken token, DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(
            BrowserCookies.Browser,
            token.Value,
            BrowserCookies.Lasting(application, until));
    }

    /// <summary>
    /// Writes what a browser the account trusts carries, so the second step is not
    /// asked for again from it (AUTH-FACT-015).
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <param name="token">The token the record answers to.</param>
    /// <param name="until">When the browser is to forget it.</param>
    /// <exception cref="ArgumentNullException">The response is absent.</exception>
    public void Trusted(HttpResponse response, OpaqueToken token, DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(
            BrowserCookies.Device,
            token.Value,
            BrowserCookies.Lasting(application, until));
    }

    /// <summary>
    /// Clears the pair, which is what a sign-out leaves behind.
    /// </summary>
    /// <param name="response">The response the browser receives.</param>
    /// <exception cref="ArgumentNullException">The response is absent.</exception>
    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(
            BrowserCookies.Session,
            BrowserCookies.Options(application, readableByScript: false));

        response.Cookies.Delete(
            BrowserCookies.Csrf,
            BrowserCookies.Options(application, readableByScript: true));
    }
}
