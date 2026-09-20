using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider as a relying party meets it: the document it advertises, the keys it
/// publishes, the code a live browser is sent back with, and the tokens the exchange
/// hands over the back channel (AUTH-OIDC-001 to AUTH-OIDC-004, AUTH-SESS-012,
/// API-REDIR-001, BFF-MACH-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class OidcFlowTests
{
    private const string Application = "browser-app";
    private const string Protocol = "mail-server";
    private const string Second = "another-browser-app";
    private const string Destination = "https://app.example.test/signin/callback";
    private const string Secret = "a-secret-the-deployment-set";
    private const string Verifier = "a-verifier-of-at-least-forty-three-characters-long";

    /// <summary>
    /// AUTH-OIDC-001 AC1: the discovery document names the endpoints the deployment
    /// answers and the flows it admits, and nothing it does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC1_TheDocumentNamesTheEndpointsAndTheFlowsAsync()
    {
        await using var deployment = new Deployment();

        Answer answered = await new Machine(deployment)
            .GetAsync("/.well-known/openid-configuration", bearer: string.Empty);

        JsonElement document = answered.Json();

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.EndsWith(
            "/oidc/authorize",
            document.GetProperty("authorization_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/token",
            document.GetProperty("token_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/userinfo",
            document.GetProperty("userinfo_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/jwks",
            document.GetProperty("jwks_uri").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(["code"], Listed(document, "response_types_supported"));
        Assert.Contains("S256", Listed(document, "code_challenge_methods_supported"));
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: what the key set serves carries the configured algorithm and
    /// the public half only, named so that a token's header resolves to one of them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_TheKeySetCarriesTheConfiguredAlgorithmAsync()
    {
        await using var deployment = new Deployment();

        Answer answered = await new Machine(deployment).GetAsync("/oidc/jwks", bearer: string.Empty);

        JsonElement key = answered.Json().GetProperty("keys")[0];

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(key.TryGetProperty("d", out _));
        Assert.Equal(1, deployment.Keys.Count);
        Assert.NotEmpty(key.GetProperty("kid").GetString()!);
    }

    /// <summary>
    /// AUTH-SESS-012 AC1 and AC2, AUTH-OIDC-002 AC1: a browser holding a live session
    /// is sent back to the client with a code and nothing else, without being asked
    /// anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC1_ALiveSessionIsSentBackWithACodeAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Authorize(Application, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(Destination + "?", Where(answered), StringComparison.Ordinal);
        Assert.NotEmpty(Returned(answered, "code"));
        Assert.Equal("the-state", Returned(answered, "state"));
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: a silent request from a browser holding no session is
    /// answered at the client's destination with what is missing, and no screen.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer answered = await new Browser(deployment)
            .SendAsync("GET", Authorize(Application, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.Equal("login_required", Returned(answered, "error"));
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: where the host declared where its sign-in screen is, a
    /// request that is not silent is forwarded to it rather than answered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync()
    {
        await using var deployment = new Deployment(
            signIn: new AuthenticationAddresses("https://janus.example.test/signin"));

        await RegisteredAsync(deployment);

        Answer answered = await new Browser(deployment)
            .SendAsync("GET", Authorize(Application, silent: false));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(
            "https://janus.example.test/signin",
            Where(answered),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// API-REDIR-001 AC1 and AC4: a destination that is not the client's registered
    /// one is replaced by it rather than refused, and the attempt is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync(
            "GET",
            Authorize(Application, silent: true, redirect: "https://attacker.test/collect"));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(Destination + "?", Where(answered), StringComparison.Ordinal);
        Assert.NotEmpty(Returned(answered, "code"));
        Assert.Contains((Microsoft.Extensions.Logging.LogLevel.Warning, 1), deployment.OidcLog.Entries);
    }

    /// <summary>
    /// AUTH-OIDC-001 AC2 and API-REDIR-001 AC1: a client the registry does not hold is
    /// refused before any destination is resolved, so nothing is forwarded anywhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_AnUnregisteredClientIsForwardedNowhereAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Authorize("nobody", silent: true));

        Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
        Assert.Null(answered.Location);
        Assert.Contains("invalid_client", answered.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-SESS-012 AC5 and AC6, AUTH-OIDC-002 AC1, BFF-MACH-001 AC3: the exchange
    /// is back channel, authenticates with the client's own secret, and hands a
    /// browser application an access token and an identity token naming the session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        string code = await CodeAsync(browser, Application);
        Answer exchanged = await new Machine(deployment).PostAsync("/oidc/token", Code(code, Application));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.NotEmpty(exchanged.Text("access_token"));
        Assert.NotEmpty(exchanged.Text("id_token"));
        Assert.False(exchanged.Json().TryGetProperty("refresh_token", out _));
        Assert.Empty(exchanged.SetCookie);
        Assert.Equal(
            Single(deployment.Sessions.All).Id.Value.ToString(),
            Claim(exchanged.Text("id_token"), "sid"));
    }

    /// <summary>
    /// AUTH-OIDC-002 AC2 and AUTH-OIDC-003 AC1: a protocol client is handed a refresh
    /// token, it rotates on use, and the one it replaced opens nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await CodeAsync(browser, Protocol);
        Answer exchanged = await machine.PostAsync("/oidc/token", Code(code, Protocol));

        string first = exchanged.Text("refresh_token");

        Assert.NotEmpty(first);

        Answer refreshed = await machine.PostAsync("/oidc/token", Refresh(first));

        Assert.Equal(StatusCodes.Status200OK, refreshed.Status);
        Assert.NotEqual(first, refreshed.Text("refresh_token"));

        Answer replayed = await machine.PostAsync("/oidc/token", Refresh(first));

        Assert.Equal(StatusCodes.Status400BadRequest, replayed.Status);
        Assert.NotEmpty(deployment.OidcAudit.Reuses);
    }

    /// <summary>
    /// AUTH-OIDC-001, chapter 09 section 9: what userinfo answers is what the scope
    /// named and nothing else, read against the published key set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_UserInfoAnswersWhatTheScopeNamesAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await CodeAsync(browser, Application);
        Answer exchanged = await machine.PostAsync("/oidc/token", Code(code, Application));
        Answer read = await machine.GetAsync("/oidc/userinfo", exchanged.Text("access_token"));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(Flow.Address, read.Text("email"));
        Assert.True(read.Json().GetProperty("email_verified").GetBoolean());
        Assert.False(read.Json().TryGetProperty("phone_number", out _));
    }

    /// <summary>
    /// BFF-SESS-003 AC2: a second application reaches the same session record without
    /// asking the person anything, so moving between applications re-establishes
    /// silently and authenticates nobody again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_003_AC2_ASecondApplicationReEstablishesSilentlyAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);

        await deployment.Clients.RecordAsync(
            new OidcClient(
                Second,
                Second,
                OidcClientKind.BrowserApplication,
                Destination,
                ["openid", "email"]),
            OpaqueToken.Of(Secret).Fingerprint(),
            TestContext.Current.CancellationToken);

        var machine = new Machine(deployment);
        string first = await CodeAsync(browser, Application);
        string second = await CodeAsync(browser, Second);

        Assert.NotEqual(first, second);

        string held = Single(deployment.Sessions.All).Id.Value.ToString();

        Assert.Equal(
            held,
            Claim((await machine.PostAsync("/oidc/token", Code(first, Application))).Text("id_token"), "sid"));
        Assert.Equal(
            held,
            Claim((await machine.PostAsync("/oidc/token", Code(second, Second))).Text("id_token"), "sid"));
    }

    /// <summary>
    /// BFF-MACH-001 AC3: the token endpoint authenticates a client that carries its
    /// secret and nothing a browser carries, and refuses one whose secret is not the
    /// registered one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC3_TheTokenEndpointAuthenticatesTheClientAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await CodeAsync(browser, Protocol);

        Answer refused = await machine.PostAsync(
            "/oidc/token",
            ("grant_type", "authorization_code"),
            ("code", code),
            ("client_id", Protocol),
            ("client_secret", "not-the-registered-secret"),
            ("redirect_uri", Destination),
            ("code_verifier", Verifier));

        Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);

        Answer taken = await machine.PostAsync("/oidc/token", Code(code, Protocol));

        Assert.Equal(StatusCodes.Status200OK, taken.Status);
        Assert.NotEmpty(taken.Text("access_token"));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC3: the registry is managed by hand, so the document offers no
    /// registration endpoint and the route a client would post one to is not there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC3_NoDynamicRegistrationEndpointExistsAsync()
    {
        await using var deployment = new Deployment();

        var machine = new Machine(deployment);
        JsonElement document = (await machine
                .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.False(document.TryGetProperty("registration_endpoint", out _));

        // A path the library maps for another method answers 405, so 404 is the route
        // not being there at all rather than this request being the wrong shape.
        Answer reached = await machine.GetAsync("/oidc/register", bearer: string.Empty);

        Assert.Equal(StatusCodes.Status404NotFound, reached.Status);
        Assert.Empty(await deployment.Clients.AllAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC4: the token a protocol client obtains is the signed-in person's
    /// and nobody else's, which is what the mail server manages app passwords under;
    /// the library holds none of those passwords.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheProtocolClientsTokenIsTheSignedInPersonsAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        string code = await CodeAsync(browser, Protocol);
        Answer exchanged = await new Machine(deployment).PostAsync("/oidc/token", Code(code, Protocol));

        Session live = Single(deployment.Sessions.All);

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(live.Subject.Value.ToString(), Claim(exchanged.Text("id_token"), "sub"));
        Assert.Equal(live.Id.Value.ToString(), Claim(exchanged.Text("id_token"), "sid"));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC3: a party that validates the token against the published keys
    /// rather than the record refuses it at its expiry and no later, so the latency it
    /// accepts is the configured lifetime.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC3_AnOfflineValidatorRefusesTheTokenAtItsExpiryAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await CodeAsync(browser, Application);
        Answer exchanged = await machine.PostAsync("/oidc/token", Code(code, Application));

        string token = exchanged.Text("access_token");

        Assert.Equal(
            StatusCodes.Status200OK,
            (await machine.GetAsync("/oidc/userinfo", token)).Status);

        deployment.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await machine.GetAsync("/oidc/userinfo", token)).Status);
    }

    /// <summary>
    /// BFF-MACH-001 AC2: a machine route refuses a request that arrives carrying a
    /// browser's session cookie.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC2_ACookieOnAMachineRouteIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        string code = await CodeAsync(browser, Application);
        string cookie = Janus.Hosting.Bff.BrowserCookies.Session
            + "="
            + browser.Cookies[Janus.Hosting.Bff.BrowserCookies.Session];

        Answer refused = await new Machine(deployment)
            .PostCarryingAsync("/oidc/token", cookie, Code(code, Application));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
    }

    private static string Challenge =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    private static string Authorize(string clientId, bool silent, string redirect = Destination) =>
        "/oidc/authorize?response_type=code"
        + "&client_id=" + Uri.EscapeDataString(clientId)
        + "&redirect_uri=" + Uri.EscapeDataString(redirect)
        + "&scope=" + Uri.EscapeDataString("openid email offline_access")
        + "&state=the-state"
        + "&code_challenge=" + Challenge
        + "&code_challenge_method=S256"
        + (silent ? "&prompt=none" : string.Empty);

    private static (string Name, string? Value)[] Code(string code, string clientId) =>
    [
        ("grant_type", "authorization_code"),
        ("code", code),
        ("client_id", clientId),
        ("client_secret", Secret),
        ("redirect_uri", Destination),
        ("code_verifier", Verifier),
    ];

    private static (string Name, string? Value)[] Refresh(string token) =>
    [
        ("grant_type", "refresh_token"),
        ("refresh_token", token),
        ("client_id", Protocol),
        ("client_secret", Secret),
    ];

    private static Session Single(IReadOnlyCollection<Session> sessions)
    {
        foreach (Session held in sessions)
        {
            return held;
        }

        throw new InvalidOperationException("The deployment holds no session.");
    }

    private static string Where(Answer answered) =>
        answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere.");

    private static string Returned(Answer answered, string name) =>
        QueryHelpers(Where(answered)).TryGetValue(name, out string? held) ? held : string.Empty;

    private static Dictionary<string, string> QueryHelpers(string where)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        int at = where.IndexOf('?', StringComparison.Ordinal);

        foreach (string pair in where[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            read[pair[..equals]] = Uri.UnescapeDataString(pair[(equals + 1)..]);
        }

        return read;
    }

    private static string Claim(string token, string name)
    {
        string payload = token.Split('.')[1];

        return JsonDocument
            .Parse(Base64Url.DecodeFromChars(payload))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;
    }

    private static List<string> Listed(JsonElement document, string name)
    {
        var read = new List<string>();

        foreach (JsonElement held in document.GetProperty(name).EnumerateArray())
        {
            read.Add(held.GetString() ?? string.Empty);
        }

        return read;
    }

    private static async Task RegisteredAsync(Deployment deployment)
    {
        foreach ((string clientId, OidcClientKind kind) in new[]
        {
            (Application, OidcClientKind.BrowserApplication),
            (Protocol, OidcClientKind.Protocol),
        })
        {
            await deployment.Clients.RecordAsync(
                new OidcClient(
                    clientId,
                    clientId,
                    kind,
                    Destination,
                    ["openid", "email", "offline_access"]),
                OpaqueToken.Of(Secret).Fingerprint(),
                TestContext.Current.CancellationToken);
        }
    }

    private static async Task<Browser> PreparedAsync(Deployment deployment)
    {
        await RegisteredAsync(deployment);

        Flow.Prepare(deployment);

        return await Flow.SignedInAsync(deployment);
    }

    private static async Task<string> CodeAsync(Browser browser, string clientId)
    {
        Answer answered = await browser.SendAsync("GET", Authorize(clientId, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);

        return Returned(answered, "code");
    }
}
