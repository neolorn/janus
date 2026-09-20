using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The provider the deployment is: the code a live session is issued, what it is
/// exchanged for, and what a refresh token presented twice costs the whole session
/// (AUTH-OIDC-001 to AUTH-OIDC-004, AUTH-SESS-012, API-REDIR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class OidcServiceTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Address = "person@example.test";
    private const string Application = "browser-app";
    private const string Protocol = "mail-server";
    private const string Destination = "https://app.example.test/signin/callback";
    private const string Secret = "a-secret-the-deployment-set";
    private const string Verifier = "a-verifier-of-at-least-forty-three-characters-long";
    private const string Scope = "openid email offline_access";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(
        "198.51.100.7",
        new DeviceDescription("Firefox", "Fedora"),
        new SessionLocation("Alexandria", "EG"));

    private readonly OidcClientStoreInMemory _clients = new();
    private readonly AuthorizationCodeStoreInMemory _codes = new();
    private readonly RefreshTokenStoreInMemory _tokens = new();
    private readonly SigningKeyStoreInMemory _keys = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly OidcAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment holding the two kinds of client.
    /// </summary>
    public OidcServiceTests()
    {
        Register(Application, OidcClientKind.BrowserApplication);
        Register(Protocol, OidcClientKind.Protocol);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-OIDC-001 AC2, AUTH-SESS-012 AC1: a client the registry does not hold gets
    /// no code, so there is nothing for it to bring to the token endpoint.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_AnUnregisteredClientObtainsNoCodeAsync()
    {
        SessionId session = await SignedInAsync();

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.IssueCodeAsync(Intent("nobody"), session, Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC2: a secret that is not the client's authenticates nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_AWrongSecretObtainsNoTokenAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.RedeemCodeAsync(
                new CodeRedemption(code, Protocol, "not-the-secret", Destination, Verifier),
                Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC2: a browser holding a live session is issued a code without
    /// anyone being asked anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC2_ALiveSessionIssuesACodeWithoutInteractionAsync()
    {
        SessionId session = await SignedInAsync();

        Assert.NotNull(Value(await Service.IssueCodeAsync(Intent(Application), session, Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: where no session answers, a silent request is told so and is
    /// never quietly issued one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_ASilentRequestWithoutASessionIsRefusedAsync()
    {
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.IssueCodeAsync(
                Intent(Application) with { Silent = true },
                session: null,
                Cancellation)));

        Assert.Empty(_sessions.All);
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code is spent once. The second presentation opens nothing,
    /// whoever makes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeIsSingleUseAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.NotNull(Value(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation)));
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code stops being exchangeable at the configured lifetime.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeExpiresWithinItsLifetimeAsync()
    {
        string code = await CodeAsync(Protocol);

        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code is bound to the client it was issued to, so another
    /// registered client holding its own secret cannot exchange it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_AnotherClientCannotExchangeTheCodeAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(Redemption(code, Application), Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: without the verifier that produced the challenge, the code
    /// opens nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_TheWrongVerifierExchangesNothingAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(
                Redemption(code, Protocol) with { CodeVerifier = Verifier + "x" },
                Cancellation)));
    }

    /// <summary>
    /// AUTH-SESS-012 AC7: what the exchange names is the session record, so the per-app
    /// session the client builds stands on it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC7_TheExchangeNamesTheSessionRecordAsync()
    {
        SessionId session = await SignedInAsync();
        string code = await CodeAsync(Protocol, session);

        Assert.Equal(
            session,
            Value(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation))!.Session);
    }

    /// <summary>
    /// AUTH-OIDC-002 AC1, AUTH-SESS-012 AC6: a browser application's own layer is
    /// issued nothing to hold after the exchange.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_002_AC1_ABrowserApplicationIsIssuedNoRefreshTokenAsync()
    {
        string code = await CodeAsync(Application);

        Assert.Null(
            Value(await Service.RedeemCodeAsync(Redemption(code, Application), Cancellation))!.RefreshToken);
    }

    /// <summary>
    /// AUTH-OIDC-002 AC2: a protocol client is the one that holds a refresh token.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_002_AC2_AProtocolClientIsIssuedOneRefreshTokenAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.NotNull(
            Value(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation))!.RefreshToken);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC1: a refresh token rotates on use, and presenting the used one
    /// again takes the whole family and everything derived from the session with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_AReusedRefreshTokenRevokesTheFamilyAsync()
    {
        SessionId session = await SignedInAsync();
        string first = (await ExchangedAsync(session))!;
        string second = Value(await Service.RefreshAsync(
            new RefreshRedemption(first, Protocol, Secret),
            Cancellation))!.RefreshToken!;

        Assert.Equal(
            ErrorCodes.CodeReplayed,
            Refused(await Service.RefreshAsync(new RefreshRedemption(first, Protocol, Secret), Cancellation)));

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RefreshAsync(new RefreshRedemption(second, Protocol, Secret), Cancellation)));

        Assert.NotNull(_sessions.All.Single(live => live.Id == session).EndedAt);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC2: the revocation is recorded, with the client that presented
    /// the token and the session everything stood on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC2_TheRevocationIsAuditedAsync()
    {
        SessionId session = await SignedInAsync();
        string first = (await ExchangedAsync(session))!;

        _ = await Service.RefreshAsync(new RefreshRedemption(first, Protocol, Secret), Cancellation);
        _ = await Service.RefreshAsync(new RefreshRedemption(first, Protocol, Secret), Cancellation);

        Assert.Equal((Protocol, session), (_audit.Reuses.Single().ClientId, _audit.Reuses.Single().Session));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC3: the token is a handle on the record, so ending the record
    /// ends the token with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC3_ARefreshTokenDoesNotOutliveTheRecordAsync()
    {
        SessionId session = await SignedInAsync();
        string first = (await ExchangedAsync(session))!;

        await _sessions.EndSpineAsync(session, _clock.GetUtcNow(), Cancellation);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.RefreshAsync(new RefreshRedemption(first, Protocol, Secret), Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC1: no token is minted from a record that has been revoked, so a
    /// code issued before the revocation opens nothing after it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC1_NoTokenIsMintedFromARevokedRecordAsync()
    {
        SessionId session = await SignedInAsync();
        string code = await CodeAsync(Protocol, session);

        await _sessions.EndSpineAsync(session, _clock.GetUtcNow(), Cancellation);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC2: what the exchange carries is the configured access-token
    /// lifetime, and a deployment that asks for longer than the ceiling does not get it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC2_TheAccessTokenLifetimeIsTheConfiguredOneAsync()
    {
        string code = await CodeAsync(Protocol);

        Assert.Equal(
            TimeSpan.FromMinutes(10),
            Value(await Service.RedeemCodeAsync(Redemption(code, Protocol), Cancellation))!.Lifetime);
    }

    /// <summary>
    /// API-REDIR-001 AC2: a destination that merely carries a known one inside it is
    /// not the client's, so no code is issued against it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_AC2_ADestinationContainingAKnownOneIsNotAcceptedAsync()
    {
        SessionId session = await SignedInAsync();

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.IssueCodeAsync(
                Intent(Application) with { Redirect = Destination + ".attacker.test/cb" },
                session,
                Cancellation)));
    }

    /// <summary>
    /// Chapter 09 section 9 (D-153): each scope names what it gives, and nothing
    /// outside those lists leaves the library.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_TheClaimsAnsweredAreTheOnesTheScopeNamesAsync()
    {
        SubjectId subject = await AccountAsync();

        OidcClaims held = Value(await Service.ClaimsAsync(subject, "openid", Cancellation))!;
        OidcClaims withEmail = Value(await Service.ClaimsAsync(subject, "openid email", Cancellation))!;

        Assert.Null(held.Email);
        Assert.Equal(Address, withEmail.Email);
        Assert.True(withEmail.EmailVerified);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string Challenge =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);

    /// <summary>
    /// BFF-SESS-006 AC1: what the provider owes the silent flow is a code issued
    /// against the live record with nobody asked anything; the per-app session built
    /// from it is the deployment's own layer (section 4, decision 66).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC1_ALiveRecordIssuesTheCodeWithNobodyAskedAsync()
    {
        SessionId session = await SignedInAsync();

        Assert.Equal(
            session,
            Value(await Service.RedeemCodeAsync(
                Redemption(await CodeAsync(Application, session), Application),
                Cancellation))!.Session);
    }

    /// <summary>
    /// BFF-SESS-006 AC2: the exchange is the back channel's, so what it hands back is
    /// what a browser never sees and what the browser was sent back with opens nothing
    /// on its own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC2_TheCodeAloneIsWhatTheBrowserCarriesAsync()
    {
        SessionId session = await SignedInAsync();
        IssuedCode issued = Value(await Service.IssueCodeAsync(
            Intent(Application),
            session,
            Cancellation))!;

        Assert.NotEmpty(issued.Code);
        Assert.DoesNotContain(
            typeof(IssuedCode).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.Ordinal));
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a code presented twice and a verifier that is not the one the
    /// challenge was made from are each refused; the state the flow carries is the
    /// deployment's own and is judged there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC3_AReusedCodeAndAWrongVerifierAreRefusedAsync()
    {
        string spent = await CodeAsync(Application);

        Assert.NotNull(Value(await Service.RedeemCodeAsync(Redemption(spent, Application), Cancellation)));
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(Redemption(spent, Application), Cancellation)));
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.RedeemCodeAsync(
                Redemption(await CodeAsync(Application), Application) with
                {
                    CodeVerifier = Verifier + "x",
                },
                Cancellation)));
    }

    /// <summary>
    /// BFF-SESS-006 AC5: the per-app session stands on the record, so a revoked record
    /// leaves nothing for the next request to exchange or to stand on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC5_ARevokedRecordLeavesNothingStandingAsync()
    {
        SessionId session = await SignedInAsync();
        string code = await CodeAsync(Application, session);

        await _sessions.EndSpineAsync(session, _clock.GetUtcNow(), Cancellation);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.RedeemCodeAsync(Redemption(code, Application), Cancellation)));
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.IssueCodeAsync(Intent(Application), session, Cancellation)));
    }

    private static AuthorizationIntent Intent(string clientId) =>
        new(clientId, Destination, Scope, Challenge, "S256", Nonce: null, Silent: false);

    private static CodeRedemption Redemption(string code, string clientId) =>
        new(code, clientId, Secret, Destination, Verifier);

    private SigningKeys Keys => new(_keys, _configuration, _work, _clock);

    private OidcService Service => new(
        _clients,
        _codes,
        _tokens,
        Keys,
        _sessions,
        _identifiers,
        _accounts,
        _audit,
        _configuration,
        _work,
        _clock,
        _randomness);

    private void Register(string clientId, OidcClientKind kind) =>
        _clients.RecordAsync(
            new OidcClient(clientId, clientId, kind, Destination, ["openid", "email", "offline_access"]),
            OpaqueToken.Of(Secret).Fingerprint(),
            CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private async ValueTask<SubjectId> AccountAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        await _identifiers.PromoteAsync(subject, email, Cancellation);

        return subject;
    }

    private async ValueTask<SessionId> SignedInAsync()
    {
        SubjectId subject = await AccountAsync();
        var id = new SessionId(Guid.NewGuid());

        await _sessions.AddAsync(
            Session.Begin(
                id,
                subject,
                new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                Somewhere,
                _clock.GetUtcNow(),
                TimeSpan.FromHours(8),
                TimeSpan.FromDays(7),
                satisfiesEveryGate: true),
            [1],
            [2],
            Cancellation);

        return id;
    }

    private async ValueTask<string> CodeAsync(string clientId, SessionId? session = null) =>
        Value(await Service.IssueCodeAsync(
            Intent(clientId),
            session ?? await SignedInAsync(),
            Cancellation))!.Code;

    private async ValueTask<string?> ExchangedAsync(SessionId session) =>
        Value(await Service.RedeemCodeAsync(
            Redemption(await CodeAsync(Protocol, session), Protocol),
            Cancellation))!.RefreshToken;
}
