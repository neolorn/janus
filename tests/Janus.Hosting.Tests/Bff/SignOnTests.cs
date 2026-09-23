using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// This application establishing its own session from the one the authentication
/// application holds: the forwarding, the return, the back-channel exchange and what
/// is left afterwards (BFF-SESS-006, BFF-SESS-003, BFF-SESS-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class SignOnTests
{
    private const string Client = "this-application";

    private const string Return = "https://janus.example.test/auth/signon/return";

    private const string Secret = "a-secret-the-deployment-set";

    private const string Page = "/account/profile";

    /// <summary>
    /// BFF-SESS-006 AC1: a browser that holds no session here, whose person holds a
    /// live record at the authentication application, ends with a session of this
    /// application's own and is never asked anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC1_ALiveRecordEstablishesASessionWithNoInteractionAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer established = await SignedOnAsync(deployment, arriving, holder);

        Assert.Equal(StatusCodes.Status302Found, established.Status);
        Assert.Equal(Page, Where(established));
        Assert.Equal(
            StatusCodes.Status200OK,
            (await arriving.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// BFF-SESS-006 AC2: the code is traded on this server's own connection, and
    /// nothing the exchange returned reaches the browser in a body, a header or a
    /// cookie.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer established = await SignedOnAsync(deployment, arriving, holder);

        Assert.Equal("/oidc/token", Assert.Single(deployment.Provider.Asked).AbsolutePath);
        Assert.Empty(established.Body);
        Assert.DoesNotContain(
            "token",
            string.Join(' ', established.SetCookie),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "token",
            string.Join(' ', established.Headers.Select(header => header.Key)),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a return presenting a state the browser was not sent out
    /// with establishes nothing and is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC3_AMismatchedStateIsRejectedAndLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        Answer refused = await arriving.SendAsync(
            "GET",
            Local(Where(issued)).Replace(
                Parameter(Where(issued), "state"),
                "a-state-of-another-browser",
                StringComparison.Ordinal));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(
            ErrorCodes.SessionCsrfInvalid.ToString(),
            refused.Json().GetProperty("code").GetString());
        Assert.Contains(
            deployment.SignOnLog.Entries,
            entry => entry.Level is LogLevel.Warning);
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a code that has already been returned once is bound to
    /// nothing the second time, so a browser cannot present it again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC3_AReturnedCodeIsNotAcceptedTwiceAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        _ = await arriving.SendAsync("GET", Local(Where(issued)));

        Answer again = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(StatusCodes.Status403Forbidden, again.Status);
        _ = Assert.Single(deployment.Provider.Asked);
    }

    /// <summary>
    /// BFF-SESS-006 AC4: what is left when the exchange is done is one session record
    /// of this application's own, and no token anywhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC4_NothingButThePerApplicationSessionIsHeldAfterwardsAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);

        _ = await SignedOnAsync(deployment, arriving, holder);

        Session established = Assert.Single(
            deployment.Sessions.All,
            session => session.Type is SessionType.PerApp);

        Assert.Equal(Spine(deployment), established.Spine);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-SESS-006 AC5: the session this application holds stands on the record, so
    /// revoking the record ends it at the next request rather than at its own expiry.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC5_RevokingTheRecordEndsThePerApplicationSessionAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);

        _ = await SignedOnAsync(deployment, arriving, holder);

        await deployment.Sessions.EndSpineAsync(
            Spine(deployment),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await arriving.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// BFF-SESS-006, AUTH-SESS-012 AC3: a browser whose person holds no record is
    /// told `login_required` once, and the second attempt asks for a sign-in, which
    /// the provider answers by forwarding to the screen the deployment declared.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_ABrowserWithNoRecordIsSentToSignInAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);

        Assert.Contains("prompt=none", Where(forwarded), StringComparison.Ordinal);

        Answer refused = await new Browser(deployment).SendAsync("GET", Local(Where(forwarded)));
        Answer again = await arriving.SendAsync("GET", Local(Where(refused)));

        Assert.Equal("login_required", Parameter(Where(refused), "error"));
        Assert.DoesNotContain("prompt=none", Where(again), StringComparison.Ordinal);

        Answer screen = await new Browser(deployment).SendAsync("GET", Local(Where(again)));

        Assert.Equal("https://janus.example.test/signin", Where(screen));
    }

    /// <summary>
    /// BFF-SESS-006, API-REDIR-001 AC1: the destination the browser returns to is the
    /// one the registry holds, and a request naming another one changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_TheDestinationIsTheRegisteredOneAndNeverAskedForAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer forwarded = await new Browser(deployment).SendAsync(
            "GET",
            Start + "&redirect_uri=" + Uri.EscapeDataString("https://attacker.test/collect"));

        Assert.Equal(Return, Parameter(Where(forwarded), "redirect_uri"));
    }

    /// <summary>
    /// BFF-SESS-006: the browser is sent back onto this application, so a return
    /// address naming another site is not one it is sent to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AReturnAddressOffThisApplicationIsNotFollowedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync(
            "GET",
            "/auth/signon?returnTo=" + Uri.EscapeDataString("https://attacker.test/collect"));
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));
        Answer established = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal("/", Where(established));
    }

    private static string Start => "/auth/signon?returnTo=" + Uri.EscapeDataString(Page);

    private static string Where(Answer answered) =>
        answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere.");

    // The deployment is one origin here, so what a browser would follow across two
    // applications is followed as a path of the one it is talking to.
    private static string Local(string where) =>
        where.StartsWith("https://janus.example.test", StringComparison.Ordinal)
            ? where["https://janus.example.test".Length..]
            : where;

    private static string Parameter(string where, string name)
    {
        int at = where.IndexOf('?', StringComparison.Ordinal);

        foreach (string pair in where[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            if (string.Equals(pair[..equals], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return string.Empty;
    }

    private static SessionId Spine(Deployment deployment)
    {
        foreach (Session held in deployment.Sessions.All)
        {
            if (held.Type is SessionType.Auth)
            {
                return held.Id;
            }
        }

        throw new InvalidOperationException("The deployment holds no record.");
    }

    // A browser at the authentication application holding the record every other
    // application's session stands on (AUTH-SESS-004).
    private static async Task<Browser> HolderAsync(Deployment deployment)
    {
        Flow.Prepare(deployment);

        return await Flow.SignedInAsync(deployment);
    }

    private static async Task RegisteredAsync(Deployment deployment) =>
        await deployment.Clients.RecordAsync(
            new OidcClient(
                Client,
                Client,
                OidcClientKind.BrowserApplication,
                Return,
                ["openid"]),
            OpaqueToken.Of(Secret).Fingerprint(),
            TestContext.Current.CancellationToken);

    // The three legs one browser makes across two applications: this application
    // forwards it, the authentication application issues the code against the record
    // it holds, and this application trades the code on its own connection.
    private static async Task<Answer> SignedOnAsync(
        Deployment deployment,
        Browser arriving,
        Browser holder)
    {
        _ = deployment;

        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        return await arriving.SendAsync("GET", Local(Where(issued)));
    }
}
