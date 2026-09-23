using System;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The pre-authentication session a browser is given the first time it arrives.
/// </summary>
/// <param name="contacts">What issues one.</param>
/// <param name="resolved">What the stage before this one established.</param>
/// <param name="cookies">Where the two values are written.</param>
/// <param name="log">Where a refusal to issue one is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-ORDER-001. It exists so that the endpoints
/// reached before a session exists have something for a synchronizer token to bind
/// to, which is what lets them be protected in exactly the way every other endpoint
/// is rather than exempted. It carries no identity and grants no access, and it is
/// never issued to a browser that already holds one or a session.
/// </remarks>
internal sealed class FirstContact(
    PreAuthenticationService contacts,
    RequestSession resolved,
    BrowserSessionCookies cookies,
    ILogger<FirstContact> log) : IMiddleware
{
    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (resolved.Live is null && resolved.FirstContact is null)
        {
            (await contacts.IssueAsync(context.RequestAborted).ConfigureAwait(false))
                .Switch(
                    issued => Given(context, issued),
                    error => BrowserProfileLog.FirstContactRefused(
                        log,
                        context.TraceIdentifier,
                        error.Code.ToString()));
        }

        await next(context).ConfigureAwait(false);
    }

    // BFF-SESS-006: the request that issued one goes on to bind a sign-on to it, so
    // what was just written is carried forward rather than read back out of the store.
    private void Given(HttpContext context, IssuedPreAuthentication issued)
    {
        cookies.Write(context.Response, issued);
        resolved.Resolved(issued.Session, issued.Secret);
    }
}
