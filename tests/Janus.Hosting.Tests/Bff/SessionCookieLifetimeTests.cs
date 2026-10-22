using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// How long the browser keeps what it is given, as a deployment writes it: a session's
/// pair until the session's absolute expiry under a policy requiring <c>aal1</c>, and the
/// pair it carries before a session for the browser's session alone (BFF-SESS-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class SessionCookieLifetimeTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, under the shipped policy, which
    /// requires <c>aal1</c>.
    /// </summary>
    public SessionCookieLifetimeTests() => Flow.Prepare(_deployment);

    /// <summary>
    /// BFF-SESS-002 AC3: first contact writes the pre-authentication cookie and the token
    /// bound to it with no expiry, and the session a completed registration signs the
    /// browser in on is written, with its token, to last until the session's absolute
    /// expiry.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_002_AC3_ASessionsPairLastsAndThePairBeforeItDoesNotAsync()
    {
        Answer contacted = await new Browser(_deployment).SendAsync("GET", "/auth/session");
        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));

        Session session = _deployment.Sessions.Behind(OpaqueToken.Of(browser.Cookies[BrowserCookies.Session]))!;
        string expires = "expires=" + session.AbsoluteExpiry.ToString("R", CultureInfo.InvariantCulture);

        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.All(
            Written(contacted, BrowserCookies.PreAuthentication, BrowserCookies.Csrf),
            written => Assert.DoesNotContain("expires=", written, StringComparison.OrdinalIgnoreCase));
        Assert.All(
            Written(completed, BrowserCookies.Session, BrowserCookies.Csrf),
            written => Assert.Contains(expires, written, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    // The cookies an answer wrote under each name, each found once.
    private static string[] Written(Answer answered, params string[] names) =>
    [
        .. names.Select(name => Assert.Single(
            answered.SetCookie,
            written => written.StartsWith(name + "=", StringComparison.Ordinal))),
    ];
}
