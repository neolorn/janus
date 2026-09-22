using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The two documents a password manager looks for at the site root, and the probe
/// that tells it whether the site answers everything (REG-PM-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class WellKnownTests
{
    private const string Probe =
        "/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200";

    private static readonly PasskeyAddresses Declared = new(
        "https://accounts.example.test/password",
        "https://accounts.example.test/passkeys/new",
        "https://accounts.example.test/passkeys");

    /// <summary>
    /// REG-PM-001 AC2: the change-password document redirects, the passkey document
    /// names both addresses, and the probe path answers as an absent path does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync()
    {
        await using var deployment = new Deployment(addresses: Declared);
        var browser = new Browser(deployment);

        Answer redirected = await browser.SendAsync("GET", "/.well-known/change-password");
        Answer passkeys = await browser.SendAsync("GET", "/.well-known/passkey-endpoints");
        Answer probed = await browser.SendAsync("GET", Probe);

        Assert.Equal(StatusCodes.Status302Found, redirected.Status);
        Assert.Equal("https://accounts.example.test/password", redirected.Location);

        Assert.Equal(StatusCodes.Status200OK, passkeys.Status);
        Assert.Equal("https://accounts.example.test/passkeys/new", passkeys.Text("enroll"));
        Assert.Equal("https://accounts.example.test/passkeys", passkeys.Text("manage"));

        Assert.Equal(StatusCodes.Status404NotFound, probed.Status);
    }

    /// <summary>
    /// REG-PM-001 AC2: both documents answer whatever the deployment declared, because
    /// the addresses are a declaration it could not have started without
    /// (LIB-HOST-001).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task MapWellKnown_TheDeclaredAddresses_AreWhatBothDocumentsCarryAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        Answer redirected = await browser.SendAsync("GET", "/.well-known/change-password");
        Answer passkeys = await browser.SendAsync("GET", "/.well-known/passkey-endpoints");

        Assert.Equal(StatusCodes.Status302Found, redirected.Status);
        Assert.Equal(StatusCodes.Status200OK, passkeys.Status);
        Assert.NotEmpty(passkeys.Text("enroll"));
        Assert.NotEmpty(passkeys.Text("manage"));
    }
}
