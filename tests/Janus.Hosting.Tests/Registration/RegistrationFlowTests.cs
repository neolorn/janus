using System;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Registration;

/// <summary>
/// The registration flow as a browser drives it: the pre-authentication session it
/// is bound to, what a browser without that cookie may do, and where the tokens are
/// (BFF-CSRF-005a, BFF-CSRF-005b, REG-SESS-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegistrationFlowTests : IAsyncDisposable
{
    private const string Client = "web";
    private const string Address = "person@example.test";
    private const string Language = "en";

    private const string Begin = "{\"clientId\":\"web\"}";

    // The steps a browser that carries nothing tries in turn.
    private static readonly string[] Steps =
    [
        "/register/identifiers",
        "/register/phone/skip",
        "/register/confirm",
        "/register/terms",
    ];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose messages carry the code and the link token, as the
    /// shipped templates do.
    /// </summary>
    public RegistrationFlowTests()
    {
        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            _deployment.Templates.Set(
                MessageKind.VerificationCode,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "code" : null, "{code} {token}"));
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// BFF-CSRF-005a AC1: a browser that carries nothing is given a first contact by
    /// the endpoint it reached, before it has anything to bind a token to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC1_FirstContactIssuesAPreAuthenticationSessionAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Assert.True(browser.Cookies.ContainsKey("__Host-janus-preauth"));
        Assert.True(browser.Cookies.ContainsKey("__Host-janus-csrf"));
        Assert.Single(_deployment.Contacts.All);
    }

    /// <summary>
    /// BFF-CSRF-005a AC2: the token of a first contact is checked exactly as a
    /// session's is, so a state change without it is refused and one with it passes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC2_TheFirstContactTokenIsValidatedLikeASessionsAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer without = await browser.SendAsync(
            "POST",
            "/register",
            Begin,
            header: true,
            token: false);

        Answer with = await browser.SendAsync("POST", "/register", ("clientId", Client));

        Assert.Equal(StatusCodes.Status403Forbidden, without.Status);
        Assert.Equal(StatusCodes.Status201Created, with.Status);
    }

    /// <summary>
    /// BFF-CSRF-005a AC4: a first contact is not a sign-in, so nothing that needs an
    /// account answers to a browser that carries only one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC4_AFirstContactCarriesNoIdentityAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status401Unauthorized, account.Status);
    }

    /// <summary>
    /// BFF-CSRF-005b AC1, REG-SESS-001 AC2: a browser that does not carry the first
    /// contact the registration was created under reaches none of it, and the stream
    /// answers it as an absent resource.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC1_AnotherBrowserReachesNoneOfTheRegistrationAsync()
    {
        Browser started = await BegunAsync();
        var elsewhere = new Browser(_deployment);

        Answer state = await elsewhere.SendAsync("GET", "/register");
        Answer age = await elsewhere.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        Answer stream = await elsewhere.SendAsync("GET", "/register/events");

        Assert.Equal(StatusCodes.Status401Unauthorized, state.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, age.Status);
        Assert.Equal(StatusCodes.Status404NotFound, stream.Status);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await started.SendAsync("GET", "/register")).Status);
    }

    /// <summary>
    /// REG-SESS-001 AC2: the refusal holds for every step of the flow, not only the
    /// one that reads the state.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_001_AC2_EveryStepIsRefusedWithoutTheFirstContactAsync()
    {
        _ = await BegunAsync();

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register/nothing");

        foreach (string step in Steps)
        {
            Answer refused = await elsewhere.SendAsync("POST", step, ("value", Address));

            Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);
        }
    }

    // A browser that has begun a registration, which is a first contact plus the
    // session the endpoint carried onto it.
    private async Task<Browser> BegunAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");
        _ = await browser.SendAsync("POST", "/register", ("clientId", Client));

        return browser;
    }
}
