using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Privacy;
using Janus.Privacy.Tests;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The cookies a deployment sets, as a version of the privacy notice keeps them: the
/// library's own at the lifetimes its configuration gives and the host's declared ones
/// (PRIV-CONS-006a, BFF-SESS-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeploymentCookiesTests
{
    private readonly ConfigurationInMemory _configuration = new();

    private readonly SignedInHintDomain _hint = new();

    /// <summary>
    /// PRIV-CONS-006a AC1, BFF-SESS-002 (D-203): the five cookies the library always sets are
    /// listed at the longest each lasts, the session and CSRF cookies at
    /// <c>session.default.absolute</c>, the pre-authentication cookie ending with the
    /// browser's session, the trusted-device and remembered-browser cookies at their own
    /// keys, none readable by a third party and none naming a purpose; every cookie the
    /// host declares is listed as declared, all of them by name.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_AC1_TheLibrarysCookiesAndEveryDeclaredOneAreListedAsync()
    {
        _configuration.Set(Settings.SessionDefaultAbsolute, TimeSpan.FromDays(180));
        _configuration.Set(Settings.FactorTrustedDeviceLifetime, TimeSpan.FromDays(14));
        _configuration.Set(Settings.DeviceVerificationLifetime, TimeSpan.FromDays(60));

        AuthorizationDeclaration declaration = new AuthorizationDeclarationBuilder()
            .Cookie("host-picks", TimeSpan.FromDays(90), thirdPartyReadable: true, "recommendations")
            .Cookie("host-tab", lifetime: null, thirdPartyReadable: false, purpose: null)
            .Build();

        IReadOnlyList<NoticeCookie> listed = (await new DeploymentCookies(_configuration, declaration, _hint)
                .ReadAsync(TestContext.Current.CancellationToken))
            .Match(cookies => cookies, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(
            [
                new NoticeCookie("__Host-identity-browser", null, TimeSpan.FromDays(60), false),
                new NoticeCookie("__Host-identity-csrf", null, TimeSpan.FromDays(180), false),
                new NoticeCookie("__Host-identity-device", null, TimeSpan.FromDays(14), false),
                new NoticeCookie("__Host-identity-preauth", null, null, false),
                new NoticeCookie("__Host-identity-session", null, TimeSpan.FromDays(180), false),
                new NoticeCookie("host-picks", "recommendations", TimeSpan.FromDays(90), true),
                new NoticeCookie("host-tab", null, null, false),
            ],
            listed);
    }

    /// <summary>
    /// PRIV-CONS-006a, BFF-SESS-002 (D-279): the signed-in hint is listed while
    /// <c>session.hint.domain</c> named a domain at the start, strictly necessary,
    /// unreadable by a third party and at <c>session.default.absolute</c>, the longest an
    /// auth session lasts; the key set since the start leaves the list as it was until a
    /// start reads it again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_TheSignedInHintIsListedWhileTheStartReadADomainAsync()
    {
        var cookies = new DeploymentCookies(_configuration, new AuthorizationDeclarationBuilder().Build(), _hint);

        _configuration.Set(Settings.SessionDefaultAbsolute, TimeSpan.FromDays(180));
        await _hint.ReadAsync(_configuration, TestContext.Current.CancellationToken);

        IReadOnlyList<NoticeCookie> unset = await ListedAsync(cookies);

        _configuration.Set(Settings.SessionSignedInDomain, "example.test");

        IReadOnlyList<NoticeCookie> unstarted = await ListedAsync(cookies);

        await _hint.ReadAsync(_configuration, TestContext.Current.CancellationToken);

        IReadOnlyList<NoticeCookie> set = await ListedAsync(cookies);

        Assert.DoesNotContain(unset, cookie => cookie.Name == "identity-signed-in");
        Assert.Equal(unset, unstarted);
        Assert.Equal(
            new NoticeCookie("identity-signed-in", null, TimeSpan.FromDays(180), false),
            Assert.Single(set, cookie => cookie.Name == "identity-signed-in"));
        Assert.Equal(unset.Count + 1, set.Count);
    }

    /// <summary>
    /// PRIV-CONS-006a (D-203): a deployment that changed none of the three keys lists its
    /// cookies at the keys' defaults, and one declaring no cookie lists the library's
    /// five alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_TheDefaultsGiveTheLifetimesOfADeploymentThatChangedNoneAsync()
    {
        IReadOnlyList<NoticeCookie> listed = (await new DeploymentCookies(
                    _configuration,
                    new AuthorizationDeclarationBuilder().Build(),
                    _hint)
                .ReadAsync(TestContext.Current.CancellationToken))
            .Match(cookies => cookies, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(
            [
                Settings.DeviceVerificationLifetime.Default,
                Settings.SessionDefaultAbsolute.Default,
                Settings.FactorTrustedDeviceLifetime.Default,
                (TimeSpan?)null,
                Settings.SessionDefaultAbsolute.Default,
            ],
            listed.Select(cookie => cookie.Lifetime));
    }

    private static async Task<IReadOnlyList<NoticeCookie>> ListedAsync(DeploymentCookies cookies) =>
        (await cookies.ReadAsync(TestContext.Current.CancellationToken))
            .Match(listed => listed, error => throw new InvalidOperationException(error.Code.ToString()));
}
