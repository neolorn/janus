using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The browser profile: what the pipeline refuses before an endpoint sees it, and
/// what it lets through (BFF-CSRF-001 to BFF-CSRF-004, BFF-CSRF-006, BFF-CSRF-007,
/// BFF-OWN-001, BFF-OWN-003, BFF-MACH-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class BrowserProfileTests : IDisposable
{
    private const string Refused = "auth.session.csrfinvalid";
    private const string Target = "https://accounts.example";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedClock _clock = new(Noon);

    private readonly SessionStoreInMemory _sessions = new();

    private readonly SessionAuditInMemory _audit = new();

    private readonly MembershipLookupInMemory _memberships = new();

    private readonly AccessGateInMemory _gate = new();

    private readonly ConfigurationInMemory _configuration = new();

    private readonly PreAuthenticationStoreInMemory _contacts = new();

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private bool _reached;

    private RequestSession? _resolved;

    /// <summary>
    /// BFF-CSRF-002 AC1: a cross-site request that would change state is refused
    /// before anything mounted after the pipeline runs.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_002_AC1_ACrossSitePostIsRejectedBeforeAnyEndpointAsync()
    {
        var log = new LogInMemory<ResourceIsolation>();
        HttpContext context = Arriving("POST", ("Sec-Fetch-Site", "cross-site"));

        await new ResourceIsolation(log).InvokeAsync(context, Endpoint);

        await AssertRefusedAsync(context);
        Assert.False(_reached);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// BFF-CSRF-002 AC3: navigation between the applications is same-site and is not
    /// what this layer is for.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_002_AC3_SameSiteNavigationIsUnaffectedAsync()
    {
        var log = new LogInMemory<ResourceIsolation>();
        HttpContext context = Arriving("GET", ("Sec-Fetch-Site", "same-site"));

        await new ResourceIsolation(log).InvokeAsync(context, Endpoint);

        Assert.True(_reached);
        Assert.Empty(log.Entries);
    }

    /// <summary>
    /// BFF-CSRF-002 AC2: a request carrying no fetch metadata is not thereby
    /// permitted; it reaches the layers that judge what it carries, and is refused
    /// there when it carries no token.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_002_AC2_WithNoFetchMetadataTheTokenIsStillValidatedAsync()
    {
        HttpContext context = Arriving("POST", (BrowserCookies.RequestHeader, "1"));

        await new ResourceIsolation(new LogInMemory<ResourceIsolation>())
            .InvokeAsync(context, Token());

        await AssertRefusedAsync(context);
    }

    /// <summary>
    /// BFF-CSRF-001 AC1, BFF-CSRF-003 AC1: the two headers are separate, and each is
    /// named on the wire as the frontend writes it. The token travels in one of them;
    /// the other carries no value and is only ever looked for.
    /// </summary>
    [Fact]
    public void BFF_CSRF_003_AC1_TheTwoHeadersAreNamedAsTheFrontendWritesThem()
    {
        Assert.Equal("X-Identity-Csrf", SynchronizerToken.Header);
        Assert.Equal("X-Identity-Request", BrowserCookies.RequestHeader);
    }

    /// <summary>
    /// BFF-CSRF-003 AC1: the custom header is required whatever else the request
    /// carries, a valid session-bound token among it.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync()
    {
        var log = new LogInMemory<CustomRequestHeader>();
        (OpaqueToken secret, OpaqueToken token) = await LiveAsync();
        HttpContext context = Arriving(
            "POST",
            ("Sec-Fetch-Site", "same-origin"),
            ("Origin", Target),
            (SynchronizerToken.Header, token.Value));

        Carrying(context, secret);

        await new CustomRequestHeader(log).InvokeAsync(context, Endpoint);

        await AssertRefusedAsync(context);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// BFF-CSRF-004 AC1: an origin that is not the one the request arrived at is
    /// refused and recorded.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_004_AC1_AMismatchedOriginIsRejectedAndLoggedAsync()
    {
        var log = new LogInMemory<OriginValidation>();
        HttpContext context = Arriving("POST", ("Origin", "https://elsewhere.example"));

        await new OriginValidation(log).InvokeAsync(context, Endpoint);

        await AssertRefusedAsync(context);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// BFF-CSRF-004: the origin the request arrived at is the expected one, so a
    /// first-party call needs nothing configured to pass.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TheOriginTheRequestArrivedAt_PassesAsync()
    {
        HttpContext context = Arriving("POST", ("Origin", Target));

        await new OriginValidation(new LogInMemory<OriginValidation>())
            .InvokeAsync(context, Endpoint);

        Assert.True(_reached);
    }

    /// <summary>
    /// BFF-CSRF-001 AC1: a state-changing request with no session, with no token, and
    /// with the token of another session are all refused and recorded.
    /// </summary>
    /// <param name="session">Whether the request carries its session.</param>
    /// <param name="token">Whether it presents a token, and whose.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(true, "another")]
    public async Task BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync(
        bool session,
        string token)
    {
        var log = new LogInMemory<SynchronizerToken>();
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        (OpaqueToken _, OpaqueToken theirs) = await LiveAsync();

        HttpContext context = string.Equals(token, "another", StringComparison.Ordinal)
            ? Arriving("POST", (SynchronizerToken.Header, theirs.Value))
            : Arriving("POST");

        if (session)
        {
            Carrying(context, secret);
        }

        await new SynchronizerToken(Tokens(), log)
            .InvokeAsync(context, Endpoint);

        await AssertRefusedAsync(context);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// BFF-CSRF-001: the token bound to the session the request arrived on is what
    /// lets it through.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TheTokenBoundToTheSession_PassesAsync()
    {
        (OpaqueToken secret, OpaqueToken token) = await LiveAsync();
        HttpContext context = Arriving("POST", (SynchronizerToken.Header, token.Value));

        Carrying(context, secret);

        await new SynchronizerToken(Tokens(), new LogInMemory<SynchronizerToken>())
            .InvokeAsync(context, Endpoint);

        Assert.True(_reached);
    }

    /// <summary>
    /// BFF-CSRF-001: a request that changes nothing needs no token, which is what
    /// makes the first load of a page possible.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ARequestThatChangesNothing_NeedsNoTokenAsync()
    {
        HttpContext context = Arriving("GET");

        await new SynchronizerToken(Tokens(), new LogInMemory<SynchronizerToken>())
            .InvokeAsync(context, Endpoint);

        Assert.True(_reached);
    }

    /// <summary>
    /// BFF-CSRF-006 AC2: rotating the session invalidates the token that stood beside
    /// the previous secret, whether presented with the old secret or the new.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_006_AC2_RotationInvalidatesThePreviousTokenAsync()
    {
        (OpaqueToken secret, OpaqueToken token) = await LiveAsync();
        Session held = (await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken))!;

        var rotated = OpaqueToken.Draw(_randomness);
        var reissued = OpaqueToken.Draw(_randomness);

        await _sessions.ReplaceSecretAsync(
            held.Id,
            rotated.Fingerprint(),
            reissued.Fingerprint(),
            TestContext.Current.CancellationToken);

        SynchronizerTokens tokens = Tokens();

        Assert.False(await tokens.MatchesAsync(rotated, token, TestContext.Current.CancellationToken));
        Assert.True(await tokens.MatchesAsync(rotated, reissued, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// BFF-SESS-004 AC1 and AC2: rotation gives the session a different identifier
    /// and the one before it resolves nothing, rather than being left orphaned.
    /// </summary>
    [Fact]
    public async Task BFF_SESS_004_AC1_RotationReplacesTheIdentifierAndInvalidatesItAsync()
    {
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        Session held = (await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken))!;

        var rotated = OpaqueToken.Draw(_randomness);

        Assert.NotEqual(secret.Value, rotated.Value);

        await _sessions.ReplaceSecretAsync(
            held.Id,
            rotated.Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        Assert.Null(await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken));
        Assert.Equal(
            held.Id,
            (await _sessions.FindByFingerprintAsync(
                rotated.Fingerprint(),
                TestContext.Current.CancellationToken))!.Id);
    }

    /// <summary>
    /// BFF-SESS-003 AC3: every application's session stands on one record, so ending
    /// the record ends all of them and none is left behind.
    /// </summary>
    [Fact]
    public async Task BFF_SESS_003_AC3_EndingTheRecordTerminatesEveryApplicationsSessionAsync()
    {
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        Session record = (await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken))!;

        foreach (int which in new[] { 1, 2 })
        {
            await _sessions.AddAsync(
                record.Derive(
                    SessionId.New(TimeProvider.System),
                    SessionType.PerApp,
                    new SessionOrigin("198.51.100." + which.ToString(System.Globalization.CultureInfo.InvariantCulture), new DeviceDescription("Firefox", "Linux")),
                    Noon,
                    TimeSpan.FromHours(8)),
                OpaqueToken.Draw(_randomness).Fingerprint(),
                OpaqueToken.Draw(_randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            3,
            (await _sessions.LiveOfAsync(
                record.Subject,
                Noon.AddHours(1),
                TestContext.Current.CancellationToken)).Count);

        await _sessions.EndSpineAsync(
            record.Id,
            Noon.AddHours(2),
            TestContext.Current.CancellationToken);

        Assert.Empty(await _sessions.LiveOfAsync(
            record.Subject,
            Noon.AddHours(3),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// BFF-OWN-001 AC1 and AC3: mounting a profile takes the pipeline and nothing
    /// else, so there is no security-relevant value to get right and every application
    /// mounts the same one implementation. Mounting a host's callback takes where it is
    /// and the provider's scheme, and nothing that could turn one of its checks off.
    /// </summary>
    [Fact]
    public void BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration()
    {
        MethodInfo[] mounting = typeof(PipelineProfiles).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Type[] callbacks = [typeof(ISignedCallback), typeof(IUnsignedCallback)];

        Assert.NotEmpty(mounting);

        foreach (MethodInfo mount in mounting)
        {
            ParameterInfo[] parameters = mount.GetParameters();

            Assert.Equal(typeof(IApplicationBuilder), parameters[0].ParameterType);

            if (mount.Name is not nameof(PipelineProfiles.UseCallback))
            {
                _ = Assert.Single(parameters);

                continue;
            }

            Assert.Equal(3, parameters.Length);
            Assert.Equal(typeof(PathString), parameters[1].ParameterType);
            Assert.Contains(parameters[2].ParameterType, callbacks);
        }
    }

    /// <summary>
    /// AUTH-SESS-007 AC1 and AC3: a state-changing request whose token is not the
    /// session's is rejected, and the rejection is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_007_AC1_AStateChangeWithoutAValidTokenIsRejectedAsync()
    {
        var log = new LogInMemory<SynchronizerToken>();
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        HttpContext context = Arriving("POST");

        Carrying(context, secret);

        await new SynchronizerToken(Tokens(), log)
            .InvokeAsync(context, Endpoint);

        await AssertRefusedAsync(context);
        Assert.False(_reached);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// AUTH-SESS-007 AC3: the entry is recorded at a level a quieter deployment does
    /// not suppress, a refused state change being a security event.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_007_AC3_TheRejectionIsLoggedAsync()
    {
        var log = new LogInMemory<SynchronizerToken>();

        await new SynchronizerToken(Tokens(), log)
            .InvokeAsync(Arriving("POST"), Endpoint);

        Assert.Equal(LogLevel.Warning, Assert.Single(log.Entries).Level);
    }

    /// <summary>
    /// AUTH-SESS-007 AC2: enforcement is the pipeline's, so nothing an endpoint
    /// carries and no key of chapter 10 section 4 takes it out of the layer. What an
    /// endpoint carries is read for enforcement in two files and only ever adds a
    /// refusal to it: an endpoint says that it needs a session, and nothing says it
    /// needs less than the stages give it (BFF-STEP-001). The third reader is the
    /// logging of BFF-LOG-002, which only ever takes a body out of a log.
    /// </summary>
    [Fact]
    public void AUTH_SESS_007_AC2_NoEndpointCanOptOut()
    {
        Assert.Equal(
            ["SensitiveBodyLogging.cs", "SessionRequired.cs", "SessionRequirement.cs"],
            Reading("GetEndpoint", "Metadata"));

        Assert.Empty(Reading("IConfigurationStore"));
    }

    /// <summary>
    /// BFF-SESS-004 AC2: the secret the session answered to before is invalidated,
    /// not left resolving an ended session alongside the new one.
    /// </summary>
    [Fact]
    public async Task BFF_SESS_004_AC2_ThePreviousIdentifierIsInvalidatedNotOrphanedAsync()
    {
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        Session held = (await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken))!;

        await _sessions.ReplaceSecretAsync(
            held.Id,
            OpaqueToken.Draw(_randomness).Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        Assert.Null(await _sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// BFF-CSRF-005 AC4: a processor that returns the browser by posting a form from
    /// its own site, with no session on it, is answered 303 with the same address and
    /// reaches nothing; the read the browser then makes of that address carries the
    /// session, the cookie a public application issues being lax (BFF-CSRF-005 AC2), and
    /// reaches the host's route. A cross-site post that carries the session, that does
    /// not navigate the page, or that loads into a frame is refused as before.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_CSRF_005_AC4_ACrossSitePostReturnContinuesAsTheHostsGetAsync()
    {
        var log = new LogInMemory<ResourceIsolation>();
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();
        (string Name, string Value)[] returning =
        [
            ("Sec-Fetch-Site", "cross-site"),
            ("Sec-Fetch-Mode", "navigate"),
            ("Sec-Fetch-Dest", "document"),
        ];

        DefaultHttpContext posted = Arriving("POST", returning);

        posted.Request.PathBase = "/host";
        posted.Request.Path = "/return";
        posted.Request.QueryString = new QueryString("?reference=r-1");

        await new ResourceIsolation(log).InvokeAsync(posted, Endpoint);

        Assert.False(_reached);
        Assert.Equal(StatusCodes.Status303SeeOther, posted.Response.StatusCode);
        Assert.Equal("/host/return?reference=r-1", posted.Response.Headers.Location.ToString());

        DefaultHttpContext continuing = Arriving("GET", returning);

        Carrying(continuing, secret);

        await new ResourceIsolation(log).InvokeAsync(continuing, Endpoint);

        Assert.True(_reached);
        Assert.Equal(
            secret.Value,
            continuing.Request.Cookies[BrowserCookies.Session]);

        _reached = false;

        DefaultHttpContext carried = Arriving("POST", returning);

        Carrying(carried, secret);

        DefaultHttpContext[] refused =
        [
            carried,
            Arriving(
                "POST",
                ("Sec-Fetch-Site", "cross-site"),
                ("Sec-Fetch-Mode", "cors"),
                ("Sec-Fetch-Dest", "empty")),
            Arriving(
                "POST",
                ("Sec-Fetch-Site", "cross-site"),
                ("Sec-Fetch-Mode", "navigate"),
                ("Sec-Fetch-Dest", "iframe")),
            Arriving("POST", ("Sec-Fetch-Site", "cross-site")),
        ];

        foreach (DefaultHttpContext context in refused)
        {
            await new ResourceIsolation(log).InvokeAsync(context, Endpoint);

            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }

        Assert.False(_reached);
    }

    /// <summary>
    /// BFF-CSRF-001 AC2 and BFF-MACH-001 AC1: no endpoint can be excluded by
    /// configuration or attribute, no stage that enforces the token reading the
    /// endpoint or its metadata; the one thing a path decides is which profile carries
    /// a request, and that is settled in the one place the library names the routes.
    /// The one other reader of the metadata is the logging of BFF-LOG-002, which
    /// enforces no token and only ever takes a body out of a log.
    /// </summary>
    [Fact]
    public void BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute()
    {
        Assert.Equal(
            ["SensitiveBodyLogging.cs", "SessionRequired.cs", "SessionRequirement.cs"],
            Reading("GetEndpoint", "Metadata"));

        Assert.Equal(["PipelineProfiles.cs"], Reading("Request.Path"));
    }

    /// <summary>
    /// BFF-MACH-001 AC1: which routes the machine profile governs is settled in one
    /// place in the library, and nothing a deployment writes reaches it, so a browser
    /// endpoint cannot be moved onto it by configuration or by an attribute.
    /// </summary>
    [Fact]
    public void BFF_MACH_001_AC1_NoBrowserEndpointCanBeMovedOntoTheMachineProfile()
    {
        Assert.Empty(Reading("MachineRoutes.Governs").Except(["PipelineProfiles.cs"]));
        Assert.Equal(["MachineRoutes.cs"], Reading("PathString[] Governed"));

        MethodInfo governs = typeof(MachineRoutes).GetMethod(
            "Governs",
            BindingFlags.Public | BindingFlags.Static)!;

        ParameterInfo only = Assert.Single(governs.GetParameters());

        Assert.Equal(typeof(PathString), only.ParameterType);
    }

    /// <summary>
    /// BFF-CSRF-001 AC3 and BFF-OWN-001 AC2: whatever the host mounts after the
    /// pipeline inherits every layer without doing anything, and a refusal is
    /// answered before it runs.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_001_AC3_AddingAnEndpointInheritsEnforcementAsync()
    {
        RequestDelegate pipeline = Mounted();
        HttpContext refused = Arriving("POST", (BrowserCookies.RequestHeader, "1"), ("Origin", Target));

        await pipeline(refused);

        await AssertRefusedAsync(refused);

        (OpaqueToken secret, OpaqueToken token) = await LiveAsync();
        HttpContext carried = Arriving(
            "POST",
            (BrowserCookies.RequestHeader, "1"),
            ("Origin", Target),
            ("Sec-Fetch-Site", "same-origin"),
            (SynchronizerToken.Header, token.Value));

        Carrying(carried, secret);

        await pipeline(carried);

        Assert.True(_reached);
    }

    /// <summary>
    /// BFF-MACH-001 AC3: a request the machine profile governs carries none of what the
    /// browser profile asks for, and passes it untouched: nothing refused, nothing issued.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_001_AC3_ARequestTheMachineProfileGovernsPassesTheBrowserProfileAsync()
    {
        RequestDelegate pipeline = Mounted();
        HttpContext context = Arriving("POST");

        context.Features.Set(MachineGoverned.Mark);

        await pipeline(context);

        Assert.True(_reached);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Empty(_contacts.All);
    }

    /// <summary>
    /// BFF-OWN-003 AC1 and AC2: the stages run in the order the contract fixes, so
    /// the cheapest rejection is the one that answers and nothing later was reached.
    /// </summary>
    [Fact]
    public async Task BFF_OWN_003_AC2_TheMountedStagesRunInTheContractsOrderAsync()
    {
        var isolation = new LogInMemory<ResourceIsolation>();
        var header = new LogInMemory<CustomRequestHeader>();
        var origin = new LogInMemory<OriginValidation>();
        var token = new LogInMemory<SynchronizerToken>();

        HttpContext context = Arriving(
            "POST",
            ("Sec-Fetch-Site", "cross-site"),
            ("Origin", "https://elsewhere.example"));

        await Mounted(isolation, header, origin, token)(context);

        await AssertRefusedAsync(context);
        Assert.Single(isolation.Entries);
        Assert.Empty(header.Entries);
        Assert.Empty(origin.Entries);
        Assert.Empty(token.Entries);
    }

    /// <summary>
    /// BFF-CSRF-007 AC1: the token is drawn from the framework's generator and
    /// compared by the framework's fixed-time comparison; nothing here writes either.
    /// </summary>
    [Fact]
    public void BFF_CSRF_007_AC1_TheTokenIsDrawnAndComparedByTheFramework()
    {
        string comparing = Repository.Authentication("Sessions/SynchronizerTokens.cs");
        string drawing = Repository.Authentication("OpaqueToken.cs");

        Assert.Contains(
            "CryptographicOperations.FixedTimeEquals",
            comparing,
            StringComparison.Ordinal);
        Assert.Contains("randomness.GetBytes", drawing, StringComparison.Ordinal);
        Assert.DoesNotContain("SequenceEqual", comparing, StringComparison.Ordinal);
    }

    /// <summary>
    /// BFF-CSRF-005a AC1: a browser that arrives holding nothing is given a
    /// pre-authentication session, so the endpoints reached before one exists have
    /// something for a token to bind to.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_005a_AC1_ABrowserHoldingNothingIsGivenAFirstContactAsync()
    {
        HttpContext context = Arriving("GET", ("Sec-Fetch-Site", "same-origin"));

        await Mounted()(context);

        Assert.True(_reached);
        Assert.Single(_contacts.All);
        Assert.Contains(
            BrowserCookies.PreAuthentication + "=",
            context.Response.Headers.SetCookie.ToString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// BFF-CSRF-005a AC2: the token bound to a first contact is validated the way a
    /// session's is, so a state change before sign-in is protected and not exempted.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_005a_AC2_TheFirstContactsTokenIsValidatedLikeASessionsAsync()
    {
        (OpaqueToken secret, OpaqueToken token) = await ContactAsync();

        HttpContext refused = Arriving(
            "POST",
            (BrowserCookies.RequestHeader, "1"),
            ("Origin", Target),
            ("Sec-Fetch-Site", "same-origin"),
            (SynchronizerToken.Header, OpaqueToken.Draw(_randomness).Value));

        CarryingFirstContact(refused, secret);

        await Mounted()(refused);

        await AssertRefusedAsync(refused);
        Assert.False(_reached);

        HttpContext carried = Arriving(
            "POST",
            (BrowserCookies.RequestHeader, "1"),
            ("Origin", Target),
            ("Sec-Fetch-Site", "same-origin"),
            (SynchronizerToken.Header, token.Value));

        CarryingFirstContact(carried, secret);

        await Mounted()(carried);

        Assert.True(_reached);
    }

    /// <summary>
    /// BFF-CSRF-005a AC4: what a first contact establishes is that a browser is the
    /// same browser, never who is using it.
    /// </summary>
    [Fact]
    public async Task BFF_CSRF_005a_AC4_AFirstContactCarriesNoIdentityAsync()
    {
        (OpaqueToken secret, OpaqueToken _) = await ContactAsync();
        HttpContext context = Arriving("GET", ("Sec-Fetch-Site", "same-origin"));

        CarryingFirstContact(context, secret);

        await Mounted()(context);

        Assert.NotNull(_resolved);
        Assert.NotNull(_resolved.FirstContact);
        Assert.Null(_resolved.Live);
        Assert.Null(_resolved.Context);
    }

    /// <summary>
    /// BFF-ORDER-001 stage 5: a cookie that no longer resolves is cleared, so the
    /// browser stops presenting it, and the request goes on with nobody on it. What a
    /// person whose session ended may reach is the endpoint's to say, and the stage
    /// that requires a session says it for all of them at once (BFF-STEP-001 AC3).
    /// </summary>
    [Fact]
    public async Task BFF_ORDER_001_AnEndedSessionIsClearedAndLeavesTheRequestAnonymousAsync()
    {
        (OpaqueToken secret, OpaqueToken _) = await LiveAsync();

        _clock.Advance(TimeSpan.FromDays(2));

        HttpContext context = Arriving("GET", ("Sec-Fetch-Site", "same-origin"));

        Carrying(context, secret);

        await Mounted()(context);

        Assert.True(_reached);
        Assert.NotNull(_resolved);
        Assert.Null(_resolved.Live);
        Assert.Equal(ErrorCodes.SessionExpired, _resolved.Expiry?.Code);
        Assert.Contains(
            BrowserCookies.Session + "=;",
            context.Response.Headers.SetCookie.ToString(),
            StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private static async Task AssertRefusedAsync(HttpContext context)
    {
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        string answered = await AnsweredAsync(context);

        Assert.Contains("\"code\":\"" + Refused + "\"", answered, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\":", answered, StringComparison.Ordinal);
    }

    private static void Carrying(HttpContext context, OpaqueToken secret) =>
        context.Request.Headers.Cookie = BrowserCookies.Session + "=" + secret.Value;

    private static void CarryingFirstContact(HttpContext context, OpaqueToken secret) =>
        context.Request.Headers.Cookie = BrowserCookies.PreAuthentication + "=" + secret.Value;

    private static async Task<string> AnsweredAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        using var reading = new StreamReader(context.Response.Body, Encoding.UTF8);

        return await reading.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext Arriving(string method, params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("accounts.example");

        foreach ((string name, string value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return context;
    }

    private Task Endpoint(HttpContext context)
    {
        _reached = true;
        _resolved = context.RequestServices?.GetService<RequestSession>();

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> Reading(params string[] what) =>
    [
        .. Repository
            .Sources()
            .Where(file => File.ReadLines(file).Any(line =>
                what.Any(named => line.Contains(named, StringComparison.Ordinal))))
            .Select(file => Path.GetFileName(file)!)
            .Order(StringComparer.Ordinal),
    ];

    private SynchronizerTokens Tokens() => new(_sessions, _contacts, _clock);

    // The layer that needs the session, as the layer before it hands a request on.
    private RequestDelegate Token() => carried =>
        new SynchronizerToken(Tokens(), new LogInMemory<SynchronizerToken>())
            .InvokeAsync(carried, Endpoint);

    private RequestDelegate Mounted() => Mounted(
        new LogInMemory<ResourceIsolation>(),
        new LogInMemory<CustomRequestHeader>(),
        new LogInMemory<OriginValidation>(),
        new LogInMemory<SynchronizerToken>());

    private RequestDelegate Mounted(
        ILogger<ResourceIsolation> isolation,
        ILogger<CustomRequestHeader> header,
        ILogger<OriginValidation> origin,
        ILogger<SynchronizerToken> token)
    {
        var services = new ServiceCollection();

        services.AddTransient<IMiddlewareFactory, MiddlewareFactory>();

        // The profile ends at the stage the authorization endpoint is answered from,
        // which the host's own registration brings; here nothing answers, and the
        // stage has to be able to stand for the layers before it to be reached.
        _ = services.AddAuthentication();

        services.AddSingleton(isolation);
        services.AddSingleton(header);
        services.AddSingleton(origin);
        services.AddSingleton(token);
        services.AddSingleton<ILogger<FirstContact>>(new LogInMemory<FirstContact>());
        services.AddSingleton<ILogger<MalformedRequest>>(new LogInMemory<MalformedRequest>());
        services.AddSingleton<ISessionStore>(_sessions);
        services.AddSingleton<ISessionAudit>(_audit);
        services.AddSingleton<IAuthenticatorStore, AuthenticatorStoreInMemory>();
        services.AddSingleton<ICredentialAudit, CredentialAuditInMemory>();
        services.AddSingleton<IMembershipLookup>(_memberships);
        services.AddSingleton<IPolicyRaiseStore, PolicyRaiseStoreInMemory>();
        services.AddSingleton<IAccessGate>(_gate);
        services.AddSingleton<IPreAuthenticationStore>(_contacts);
        services.AddSingleton<IConfigurationStore>(_configuration);
        services.AddSingleton<IUnitOfWork, UnitOfWorkInMemory>();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(_randomness);
        services.AddSingleton(new BrowserSessionCookies(ApplicationKind.Public));
        services.AddSingleton<ILocationResolver, LocationResolverInMemory>();
        services.AddScoped<PolicyResolution>();
        services.AddSingleton<IAdministrativeOrganization>(new AdministrativeOrganizationInMemory());
        services.AddScoped<AdministrativeScope>();
        services.AddScoped<SessionService>();
        services.AddScoped<PreAuthenticationService>();
        services.AddScoped<SynchronizerTokens>();
        services.AddScoped<MalformedRequest>();
        services.AddScoped<ResourceIsolation>();
        services.AddScoped<CustomRequestHeader>();
        services.AddScoped<OriginValidation>();
        services.AddScoped<RequestSession>();
        services.AddScoped<SessionResolution>();
        services.AddScoped<FirstContact>();
        services.AddScoped<SynchronizerToken>();
        services.AddScoped<SessionRequirement>();

        ServiceProvider provider = services.BuildServiceProvider();
        var building = new ApplicationBuilder(provider);

        _ = building.UseBrowserProfile();
        _ = building.Use(_ => Endpoint);

        RequestDelegate built = building.Build();

        return async context =>
        {
            // One scope per request, as a host gives each request its own, so that
            // what one request resolved is not what the next one reads.
            await using AsyncServiceScope scope = provider.CreateAsyncScope();

            context.RequestServices = scope.ServiceProvider;

            await built(context);
        };
    }

    private async Task<(OpaqueToken Secret, OpaqueToken Token)> ContactAsync()
    {
        var secret = OpaqueToken.Draw(_randomness);
        var token = OpaqueToken.Draw(_randomness);

        await _contacts.AddAsync(
            PreAuthentication.Issue(secret, token, Noon, TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);

        return (secret, token);
    }

    private async Task<(OpaqueToken Secret, OpaqueToken Token)> LiveAsync()
    {
        var secret = OpaqueToken.Draw(_randomness);
        var token = OpaqueToken.Draw(_randomness);

        await _sessions.AddAsync(
            Session.Begin(
                SessionId.New(TimeProvider.System),
                SubjectId.New(_randomness),
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                Noon,
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(30),
                satisfiesEveryGate: false),
            secret.Fingerprint(),
            token.Fingerprint(),
            TestContext.Current.CancellationToken);

        return (secret, token);
    }
}
