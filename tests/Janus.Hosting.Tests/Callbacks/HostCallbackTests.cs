using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Callbacks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Janus.Hosting.Tests.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Callbacks;

/// <summary>
/// The machine profile as the seam a host mounts its own callbacks on, with one signed
/// and one unsigned callback of a fake provider mounted the way a host mounts them
/// (BFF-MACH-001 to BFF-MACH-003, INT-GEN-003, chapter 09 section 10).
/// </summary>
[Trait("kind", "unit")]
public sealed class HostCallbackTests : IAsyncDisposable
{
    private const string Events = "/hooks/events";

    private const string Status = "/hooks/status";

    private const string Secret = "the-current-secret";

    private const string Body = "{\"id\":\"evt-0001\",\"state\":\"active\"}";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly IPAddress Provider = IPAddress.Parse("203.0.113.9");

    private readonly ConfigurationInMemory _configuration = new();
    private readonly CallbackLedgerInMemory _callbacks = new();
    private readonly CallbackEventsInMemory _claims = new();
    private readonly CallbackReferenceStoreInMemory _references = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly LogInMemory<SignedCallbackGuard> _signedLog = new();
    private readonly LogInMemory<UnsignedCallbackGuard> _unsignedLog = new();
    private readonly SignedHostCallback _signed = new();
    private readonly UnsignedHostCallback _unsigned = new();
    private readonly ServiceProvider _provider;
    private readonly RequestDelegate _pipeline;

    private int _reached;

    private int _answering = StatusCodes.Status200OK;

    /// <summary>
    /// The host's pipeline: its two callbacks mounted on the machine profile, then its
    /// own routes, which answer whatever a test says they do.
    /// </summary>
    public HostCallbackTests()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfigurationStore>(_configuration);
        services.AddSingleton<ICallbackLedger>(_callbacks);
        services.AddSingleton<ICallbackEvents>(_claims);
        services.AddSingleton<ICallbackReferenceStore>(_references);
        services.AddSingleton<IUnitOfWork>(_work);
        services.AddSingleton<IEvents>(_events);
        services.AddSingleton<IAlertChannels>(_events);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(_randomness);
        services.AddScoped<CallbackAdmission>();
        services.AddScoped<CallbackReferences>();
        services.AddSingleton<ILogger<SignedCallbackGuard>>(_signedLog);
        services.AddSingleton<ILogger<UnsignedCallbackGuard>>(_unsignedLog);
        services.AddSingleton<ILogger<MalformedRequest>>(new LogInMemory<MalformedRequest>());
        services.AddSingleton<ILogger<MachineProfile>>(new LogInMemory<MachineProfile>());
        services.AddTransient<IMiddlewareFactory, MiddlewareFactory>();

        // The writer logs every refusal it answers (BFF-LOG-001), through the logging
        // every host registers.
        services.AddLogging();
        services.AddScoped<MalformedRequest>();
        services.AddScoped<MachineProfile>();

        _provider = services.BuildServiceProvider();

        var building = new ApplicationBuilder(_provider);

        _ = building.UseCallback(new PathString(Events), _signed);
        _ = building.UseCallback(new PathString(Status), _unsigned);
        _ = building.Use(_ => HostRouteAsync);

        _pipeline = building.Build();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// BFF-MACH-002 AC1: a request carrying no signature, and one whose signature is not
    /// the one the secret gives its bytes, are rejected before anything parses the body.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_AC1_AnUnsignedOrMisSignedCallbackIsRejectedBeforeParsingAsync()
    {
        HttpContext unsigned = await SentAsync(Events, Body, headers: []);
        HttpContext misSigned = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, "a-guessed-secret"));
        HttpContext altered = await SentAsync(
            Events,
            Body.Replace("active", "revoked", StringComparison.Ordinal),
            SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.All(
            new[] { unsigned, misSigned, altered },
            context => Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode));
        Assert.Equal(0, _signed.Parsed);
        Assert.Equal(0, _reached);
    }

    /// <summary>
    /// BFF-MACH-002 AC2: a delivery signed more than five minutes before it arrives is
    /// rejected; one inside the window is carried.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_AC2_AReplayOutsideTheWindowIsRejectedAsync()
    {
        HttpContext replayed = await SentAsync(
            Events,
            Body,
            SignedHostCallback.Signing(Body, Noon - TimeSpan.FromMinutes(6), Secret));

        Assert.Equal(StatusCodes.Status429TooManyRequests, replayed.Response.StatusCode);
        Assert.Equal(0, _reached);

        HttpContext timely = await SentAsync(
            Events,
            Body,
            SignedHostCallback.Signing(Body, Noon - TimeSpan.FromMinutes(4), Secret));

        Assert.Equal(StatusCodes.Status200OK, timely.Response.StatusCode);
        Assert.Equal(1, _reached);
    }

    /// <summary>
    /// BFF-MACH-002 AC3: the provider delivering the same event twice reaches the host's
    /// route once, and the second delivery is acknowledged so the provider stops.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_AC3_ADuplicateEventIdentifierIsProcessedOnceAsync()
    {
        HttpContext first = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));
        HttpContext second = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        Assert.Equal(1, _reached);
        Assert.Contains(_signedLog.Entries, entry => entry is (LogLevel.Information, 2));
    }

    /// <summary>
    /// BFF-MACH-002 AC3: a delivery the host's route did not carry through gives its
    /// claim back, so the provider's next delivery of the event is carried rather than
    /// taken for a duplicate.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_AC3_ADeliveryTheRouteFailedIsCarriedAgainAsync()
    {
        _answering = StatusCodes.Status503ServiceUnavailable;

        _ = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(0, _claims.Held);

        _answering = StatusCodes.Status200OK;

        _ = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(2, _reached);
        Assert.Equal(1, _claims.Held);
    }

    /// <summary>
    /// BFF-MACH-002 AC4: the signature is compared by the framework's fixed-time
    /// comparison, against every live secret whichever matches, and never by an
    /// ordinary comparison.
    /// </summary>
    [Fact]
    public void BFF_MACH_002_AC4_ComparisonIsConstantTime()
    {
        string guard = Repository.Hosting("Callbacks/SignedCallbackGuard.cs");

        Assert.Contains("CryptographicOperations.FixedTimeEquals", guard, StringComparison.Ordinal);
        Assert.Contains("matched |=", guard, StringComparison.Ordinal);
        Assert.DoesNotContain("SequenceEqual", guard, StringComparison.Ordinal);
        Assert.DoesNotContain("matched ||", guard, StringComparison.Ordinal);
    }

    /// <summary>
    /// BFF-MACH-002: a rotated secret verifies beside its successor for 24 hours and
    /// not after.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_TheSecretReplacedVerifiesFor24HoursAsync()
    {
        _signed.Secrets = new CallbackSecrets(
            Encoding.UTF8.GetBytes("the-next-secret"),
            Noon - TimeSpan.FromHours(23),
            Encoding.UTF8.GetBytes(Secret));

        HttpContext overlapping = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status200OK, overlapping.Response.StatusCode);

        _clock.Advance(TimeSpan.FromHours(2));

        string later = Body.Replace("evt-0001", "evt-0002", StringComparison.Ordinal);
        HttpContext expired = await SentAsync(Events, later, SignedHostCallback.Signing(later, Noon + TimeSpan.FromHours(2), Secret));
        HttpContext current = await SentAsync(
            Events,
            later,
            SignedHostCallback.Signing(later, Noon + TimeSpan.FromHours(2), "the-next-secret"));

        Assert.Equal(StatusCodes.Status429TooManyRequests, expired.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, current.Response.StatusCode);
        Assert.Equal(2, _reached);
    }

    /// <summary>
    /// BFF-MACH-002: secrets the secrets manager cannot give verify nothing, so a
    /// delivery that would have held is refused rather than carried unverified.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_SecretsTheManagerCannotGiveVerifyNothingAsync()
    {
        _signed.Secrets = null;

        HttpContext unverified = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status429TooManyRequests, unverified.Response.StatusCode);
        Assert.Equal(0, _signed.Parsed);
        Assert.Equal(0, _reached);
    }

    /// <summary>
    /// BFF-MACH-002 AC3: idempotency is keyed on the provider's event identifier, so a
    /// delivery whose signature holds but which carries none is refused rather than
    /// carried unkeyed.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_002_AC3_ADeliveryCarryingNoEventIdentifierIsRefusedAsync()
    {
        const string unkeyed = "{\"state\":\"active\"}";

        HttpContext refused = await SentAsync(Events, unkeyed, SignedHostCallback.Signing(unkeyed, Noon, Secret));

        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Response.StatusCode);
        Assert.Equal(1, _signed.Parsed);
        Assert.Equal(0, _claims.Held);
        Assert.Equal(0, _reached);
    }

    /// <summary>
    /// BFF-MACH-002: the provider's algorithm is one the platform computes a keyed hash
    /// with, or the host finds out when it mounts the callback.
    /// </summary>
    [Fact]
    public void BFF_MACH_002_AnAlgorithmThePlatformCannotComputeFailsWhenMounted()
    {
        var unknown = new SignedHostCallback { Algorithm = new HashAlgorithmName("NOT-A-HASH") };

        _ = Assert.ThrowsAny<CryptographicException>(
            () => new ApplicationBuilder(_provider).UseCallback(new PathString(Events), unknown));
    }

    /// <summary>
    /// INT-GEN-003: a source past <c>integration.callback.ratelimit</c> in the minute is
    /// answered 429 with the interval before anything is read or looked up.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_GEN_003_AFloodIsAnsweredBeforeAnyLookupAsync()
    {
        _configuration.Set(Settings.IntegrationCallbackRateLimit, 1);

        string reference = await IssuedAsync();

        HttpContext admitted = await SentAsync(Status, "{}", headers: [], query: "?reference=" + reference);
        HttpContext flooded = await SentAsync(Status, "{}", headers: [], query: "?reference=" + reference);

        Assert.Equal(StatusCodes.Status200OK, admitted.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, flooded.Response.StatusCode);
        Assert.Equal("60", flooded.Response.Headers.RetryAfter.ToString());
        Assert.Equal(1, _references.Looked);
        Assert.Equal(1, _unsigned.Asked);
        Assert.Equal(1, _reached);
    }

    /// <summary>
    /// INT-GEN-003: where the provider publishes the ranges it calls from, a callback
    /// from outside them is rejected and one from inside is carried.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_GEN_003_ACallbackFromOutsideThePublishedRangesIsRejectedAsync()
    {
        _signed.Sources = [IPNetwork.Parse("198.51.100.0/24")];

        HttpContext outside = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status429TooManyRequests, outside.Response.StatusCode);
        Assert.Equal(0, _reached);

        _signed.Sources = [IPNetwork.Parse("203.0.113.0/24")];

        HttpContext inside = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status200OK, inside.Response.StatusCode);
        Assert.Equal(1, _reached);
    }

    /// <summary>
    /// BFF-MACH-003 AC1 and chapter 09 section 10 AC2: an unsigned callback carrying an
    /// issued reference reaches the host's route only once the provider's API confirms
    /// it; an unconfirmed one advances nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_003_AC1_NoUnsignedCallbackAdvancesStateWithoutConfirmationAsync()
    {
        string reference = await IssuedAsync();

        _unsigned.Confirms = false;

        HttpContext unconfirmed = await SentAsync(Status, "{}", headers: [], query: "?reference=" + reference);

        Assert.Equal(StatusCodes.Status429TooManyRequests, unconfirmed.Response.StatusCode);
        Assert.Equal(0, _reached);

        _unsigned.Confirms = true;

        HttpContext confirmed = await SentAsync(Status, "{}", headers: [], query: "?reference=" + reference);

        Assert.Equal(StatusCodes.Status200OK, confirmed.Response.StatusCode);
        Assert.Equal(2, _unsigned.Asked);
        Assert.Equal(1, _reached);
    }

    /// <summary>
    /// BFF-MACH-003 AC2 and chapter 09 section 10 AC1: a forged callback carrying a
    /// reference of the right shape that was never issued, or one issued for another
    /// callback, is rejected, logged, and never put to the provider.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_003_AC2_AForgedCallbackWithAGuessedReferenceIsRejectedAndLoggedAsync()
    {
        byte[] guessed = new byte[16];

        _randomness.GetBytes(guessed);

        string elsewhere = (await new CallbackReferences(_references, _work, _randomness, _clock)
                .IssueAsync("another-callback", TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        HttpContext forged = await SentAsync(
            Status,
            "{}",
            headers: [],
            query: "?reference=" + Base64Url.EncodeToString(guessed));
        HttpContext misdirected = await SentAsync(Status, "{}", headers: [], query: "?reference=" + elsewhere);

        Assert.Equal(StatusCodes.Status429TooManyRequests, forged.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, misdirected.Response.StatusCode);
        Assert.Equal(2, _unsignedLog.Entries.Count(entry => entry is (LogLevel.Warning, 1)));
        Assert.Equal(0, _unsigned.Asked);
        Assert.Equal(0, _reached);
    }

    /// <summary>
    /// BFF-MACH-003 AC3: rejections from one source past
    /// <c>alerting.callback.threshold</c> in the hour raise
    /// <c>callback-verification-failed</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_003_AC3_RepeatedVerificationFailuresRaiseAnAlertAsync()
    {
        _configuration.Set(Settings.AlertingCallbackThreshold, 2);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            _ = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, "a-guessed-secret"));
        }

        Assert.Empty(_events.Of<AlertRaised>());

        _ = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, "a-guessed-secret"));

        Assert.Equal(
            AlertCondition.CallbackVerificationFailed,
            Assert.Single(_events.Of<AlertRaised>()).Condition);
    }

    /// <summary>
    /// BFF-MACH-001 AC2 and AC3: a host's callback authenticates by what its provider
    /// sends, and one carrying a browser's session cookie is refused before its checks.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC2_AHostCallbackCarryingASessionCookieIsRefusedAsync()
    {
        HttpContext carried = await SentAsync(
            Events,
            Body,
            [.. SignedHostCallback.Signing(Body, Noon, Secret), ("Cookie", BrowserCookies.Session + "=stale")]);

        Assert.Equal(StatusCodes.Status403Forbidden, carried.Response.StatusCode);
        Assert.Empty(_callbacks.Counted);
        Assert.Equal(0, _reached);

        HttpContext genuine = await SentAsync(Events, Body, SignedHostCallback.Signing(Body, Noon, Secret));

        Assert.Equal(StatusCodes.Status200OK, genuine.Response.StatusCode);
        Assert.NotNull(genuine.Features.Get<MachineGoverned>());
    }

    /// <summary>
    /// INT-GEN-003: a correlation reference is 128 random bits in base64url, and what
    /// the library keeps of it is its hash, never itself.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_GEN_003_AReferenceIs128RandomBitsKeptByItsHashAsync()
    {
        string reference = await IssuedAsync();

        Assert.Equal(16, Base64Url.DecodeFromChars(reference).Length);
        Assert.Equal(
            SHA256.HashData(Encoding.UTF8.GetBytes(reference)),
            Assert.Single(_references.Kept));
    }

    private async Task<string> IssuedAsync() =>
        (await new CallbackReferences(_references, _work, _randomness, _clock)
            .IssueAsync(_unsigned.Name, TestContext.Current.CancellationToken))
        .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private async Task<HttpContext> SentAsync(
        string path,
        string body,
        (string Name, string Value)[] headers,
        string query = "")
    {
        await using AsyncServiceScope scope = _provider.CreateAsyncScope();

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        byte[] bytes = Encoding.UTF8.GetBytes(body);

        context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        context.Connection.RemoteIpAddress = Provider;
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example.test");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query.Length == 0 ? null : query);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Response.Body = new MemoryStream();

        foreach ((string name, string value) in headers)
        {
            context.Request.Headers.Append(name, value);
        }

        await _pipeline(context);

        return context;
    }

    // The host's own route: it reads the body again, as it would to act on it.
    private async Task HostRouteAsync(HttpContext context)
    {
        using var reading = new StreamReader(context.Request.Body, Encoding.UTF8);

        _ = await reading.ReadToEndAsync(context.RequestAborted);

        _reached++;
        context.Response.StatusCode = _answering;
    }
}
