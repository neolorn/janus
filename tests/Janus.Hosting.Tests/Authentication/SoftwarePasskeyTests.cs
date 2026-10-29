using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core.Configuration;
using Janus.Testing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authentication;

/// <summary>
/// The testing package's software passkey driven through the library's own ceremonies,
/// as a host's test drives it (LIB-TEST-003, AUTH-FACT-014).
/// </summary>
[Trait("kind", "unit")]
public sealed class SoftwarePasskeyTests : IAsyncDisposable
{
    private const string Session = "__Host-identity-session";

    private static readonly Uri Origin = new("https://identity.example.test");

    private readonly Deployment _deployment = new();

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment whose messages carry the code and the link token, as the shipped
    /// templates do.
    /// </summary>
    public SoftwarePasskeyTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// LIB-TEST-003, AUTH-FACT-014: the software passkey answers the creation options of
    /// the registration ceremony with a credential the library verifies and records as a
    /// synced ES256 passkey, backup eligible and backed up, and answers the request
    /// options of a sign-in with an assertion that completes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_ASoftwarePasskeyEnrolsAndSignsInThroughTheLibrarysCeremoniesAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        using var passkey = new SoftwarePasskey(Origin, _randomness);
        Browser registering = await Flow.ConfirmedAsync(_deployment);
        Answer begun = await registering.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey"));

        Answer created = await registering.SendAsync(
            "POST",
            "/auth/webauthn/register/complete",
            new JsonObject { ["credential"] = passkey.Create(begun.Json()), ["label"] = "This phone" }.ToJsonString());
        Answer completed = await registering.SendAsync(
            "POST",
            "/register/terms",
            Flow.TermsVersion(_deployment),
            Flow.NoticeVersion(_deployment));

        Flow.Carried(_deployment);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        Authenticator held = Assert.Single(_deployment.Authenticators.All);
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer signedIn = await browser.SendAsync(
            "POST",
            "/auth/factor",
            new JsonObject
            {
                ["challengeId"] = began.Text("challengeId"),
                ["factor"] = "passkey",
                ["value"] = passkey.Get(began.Json().GetProperty("webauthn")),
            }.ToJsonString());

        Assert.Equal(StatusCodes.Status200OK, created.Status);
        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.Equal(-7, held.WebAuthn!.Algorithm);
        Assert.Equal(passkey.CredentialId, Base64Url.EncodeToString(held.WebAuthn.CredentialId.Span));
        Assert.True(held.WebAuthn.BackupEligible);
        Assert.True(held.WebAuthn.BackupState);
        Assert.Equal(StatusCodes.Status200OK, signedIn.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.True(browser.Cookies.ContainsKey(Session));
    }
}
