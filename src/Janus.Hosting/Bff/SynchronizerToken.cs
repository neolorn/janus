using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The synchronizer token, validated against the session the request arrived on.
/// </summary>
/// <param name="tokens">What says whether a token belongs to a session.</param>
/// <param name="resolved">What the session resolution stage established.</param>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-001 and BFF-CSRF-006. Enforcement is here and nowhere else:
/// an endpoint is protected because of where it is mounted, so a new one inherits
/// this without doing anything and no attribute or key excludes an old one. The
/// frontend reads the token from the cookie it was issued in and presents it in a
/// header, which the browser never sets by itself. A request whose session cookie
/// names no live session goes on as one that carried none, so the token bound to its
/// first contact is taken from it as well: the cookie of a session the restore in
/// place admits is kept, and the two restore routes are carried by that token (D-226).
/// </remarks>
internal sealed class SynchronizerToken(
    SynchronizerTokens tokens,
    RequestSession resolved,
    ILogger<SynchronizerToken> log) : IMiddleware
{
    /// <summary>
    /// The header the token is presented in.
    /// </summary>
    public const string Header = "X-Identity-Csrf";

    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (StateChange.Changes(context.Request)
            && !await BoundAsync(context, context.RequestAborted).ConfigureAwait(false))
        {
            BrowserProfileLog.TokenRejected(log, context.TraceIdentifier, context.Request.Method);
            await Refusal
                .WriteAsync(context, ErrorCodes.SessionCsrfInvalid, context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private async ValueTask<bool> BoundAsync(HttpContext context, CancellationToken cancellationToken)
    {
        string presented = context.Request.Headers[Header].ToString();

        if (presented.Length is 0)
        {
            return false;
        }

        // BFF-CSRF-005a AC2: a browser that holds no session presents the token bound
        // to its first contact instead, and it is validated the same way.
        string secret = context.Request.Cookies[BrowserCookies.Session] ?? string.Empty;

        if (secret.Length is not 0)
        {
            bool bound = await tokens
                .MatchesAsync(OpaqueToken.Of(secret), OpaqueToken.Of(presented), cancellationToken)
                .ConfigureAwait(false);

            // BFF-ORDER-001 stage 5 (D-226): a cookie that names no live session leaves
            // the request as one that carried none, whose token is its first contact's.
            if (bound || resolved.Live is not null)
            {
                return bound;
            }
        }

        string first = context.Request.Cookies[BrowserCookies.PreAuthentication] ?? string.Empty;

        return first.Length is not 0
            && await tokens
                .MatchesFirstContactAsync(
                    OpaqueToken.Of(first),
                    OpaqueToken.Of(presented),
                    cancellationToken)
                .ConfigureAwait(false);
    }
}
