using System;
using System.Collections.Frozen;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Configuration;

/// <summary>
/// <c>GET /settings</c>: the keys, providers and purposes a frontend renders from, and
/// the policy in force for whoever the browser carries (D-224).
/// </summary>
[Trait("kind", "unit")]
public sealed class FrontendSettingsTests : IAsyncDisposable
{
    private static readonly OrganizationId Strict =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, with one organization whose policy is
    /// stricter than the system's.
    /// </summary>
    public FrontendSettingsTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            Strict.ToString(),
            new PolicyOverride(
                AssuranceLevel.Aal2,
                new[] { Factor.Passkey }.ToFrozenSet(),
                null,
                null,
                null,
                null,
                null));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// D-224 item 15: a browser that carries nothing reads each key as it stands, the
    /// providers declared, the purposes a person decides and the system policy, and
    /// nothing a cache may keep.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_001_ABrowserCarryingNothingReadsTheKeysAndTheSystemPolicyAsync()
    {
        _deployment.Configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Required);
        _deployment.Configuration.Set(Settings.IdentifiersEmailMax, 4);
        _deployment.Configuration.Set(Settings.ProfileDateOfBirth, AttributeRequirement.Optional);
        _deployment.Configuration.Set(Settings.PasswordFloorSingleFactor, 16);

        Answer read = await new Browser(_deployment).SendAsync("GET", "/settings");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("no-store", read.Header("Cache-Control"));

        JsonElement settings = read.Json();

        Assert.Equal("required", settings.GetProperty("registration").GetProperty("phone").GetString());
        Assert.Equal(4, settings.GetProperty("identifiers").GetProperty("emailMax").GetInt32());
        Assert.Equal(
            Settings.IdentifiersPhoneMax.Default,
            settings.GetProperty("identifiers").GetProperty("phoneMax").GetInt32());
        Assert.Equal(
            Settings.IdentifiersUsernameEnabled.Default,
            settings.GetProperty("identifiers").GetProperty("usernameEnabled").GetBoolean());
        Assert.Equal("off", settings.GetProperty("profile").GetProperty("legalName").GetString());
        Assert.Equal("optional", settings.GetProperty("profile").GetProperty("dateOfBirth").GetString());
        Assert.Equal(16, settings.GetProperty("password").GetProperty("floorSingleFactor").GetInt32());
        Assert.Equal(
            Settings.PasswordFloorWithMfa.Default,
            settings.GetProperty("password").GetProperty("floorWithMfa").GetInt32());
        Assert.Equal(
            Settings.PasswordMaximum.Default,
            settings.GetProperty("password").GetProperty("maximum").GetInt32());
        Assert.Equal(
            ["apple", "google"],
            settings.GetProperty("providers").EnumerateArray().Select(provider => provider.GetString()).Order());
        Assert.Equal(
            "aal1",
            settings.GetProperty("policy").GetProperty("requiredAssurance").GetString());
        Assert.Equal(
            ["identifiers", "password", "policy", "profile", "providers", "purposes", "registration"],
            settings.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["loginFactors", "photos", "requiredAssurance"],
            settings.GetProperty("policy").EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// D-224 item 15 and PRIV-BASIS-003: each purpose resting on consent or on an
    /// objectable basis is answered with what it takes, its consent kind and its
    /// governing document; a purpose on any other basis is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_003_ThePurposesAPersonDecidesAreAnsweredWithWhatEachTakesAsync()
    {
        Answer read = await new Browser(_deployment).SendAsync("GET", "/settings");

        (string?, string?, string?, string?)[] purposes =
        [
            .. read.Json().GetProperty("purposes").EnumerateArray().Select(purpose => (
                purpose.GetProperty("purpose").GetString(),
                purpose.GetProperty("takes").GetString(),
                purpose.GetProperty("consentKind").ValueKind is JsonValueKind.Null
                    ? null
                    : purpose.GetProperty("consentKind").GetString(),
                purpose.GetProperty("document").GetString())),
        ];

        Assert.Equal(
            [
                ("marketing", "consent", "ordinary", "privacy-notice"),
                ("newsletter", "consent", "ordinary", Janus.Privacy.Tests.Declaration.Newsletter),
                ("recommendations", "consent", "written", "privacy-notice"),
                ("security", "objection", null, "privacy-notice"),
            ],
            purposes);
    }

    /// <summary>
    /// D-224 item 15 and AUTH-PRIN-002: the policy is the one in force for the account
    /// whose session the request carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PRIN_002_TheSettingsCarryThePolicyInForceForTheSignedInAccountAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Memberships.Place(_deployment.Directory.Created[^1].Subject, Strict);

        Answer read = await browser.SendAsync("GET", "/settings");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("aal2", read.Json().GetProperty("policy").GetProperty("requiredAssurance").GetString());
        Assert.Equal(
            ["passkey"],
            read.Json().GetProperty("policy").GetProperty("loginFactors").EnumerateArray().Select(factor => factor.GetString()));
    }

    /// <summary>
    /// D-224 item 15 and REG-INV-001: a registration opened at an invitation reads the
    /// policy of the organization it joins, before any membership exists.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_001_ARegistrationAtAnInvitationReadsThePolicyOfTheOrganizationItJoinsAsync()
    {
        using var randomness = RandomNumberGenerator.Create();
        var token = OpaqueToken.Draw(randomness);

        _deployment.Invitations.Held.Add(Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            Strict,
            SubjectId.New(randomness),
            new InvitedIdentifiers(Email: null, Phone: null, CorporateEmail: null),
            roles: [],
            documents: [],
            mailbox: null,
            token.Fingerprint(),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(7)));

        var browser = new Browser(_deployment);
        _ = await browser.SendAsync("GET", "/register");
        Answer begun = await browser.SendAsync(
            "POST",
            "/register",
            ("clientId", "web"),
            ("invitationToken", token.Value));

        Assert.Equal(StatusCodes.Status201Created, begun.Status);

        Answer read = await browser.SendAsync("GET", "/settings");

        Assert.Equal("aal2", read.Json().GetProperty("policy").GetProperty("requiredAssurance").GetString());

        Answer elsewhere = await new Browser(_deployment).SendAsync("GET", "/settings");

        Assert.Equal(
            "aal1",
            elsewhere.Json().GetProperty("policy").GetProperty("requiredAssurance").GetString());
    }

    /// <summary>
    /// REG-INV-001, AUTH-PRIN-002: where the policy that governs a registration does not
    /// read, the fault names what was read: the policy of the invitation's organization
    /// for a registration opened at an invitation, and the key <c>policy.default</c> for
    /// one opened without.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_001_AnUnreadablePolicyIsNamedByWhatTheRegistrationReadAsync()
    {
        using var randomness = RandomNumberGenerator.Create();
        var token = OpaqueToken.Draw(randomness);

        _deployment.Invitations.Held.Add(Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            Strict,
            SubjectId.New(randomness),
            new InvitedIdentifiers(Email: null, Phone: null, CorporateEmail: null),
            roles: [],
            documents: [],
            mailbox: null,
            token.Fingerprint(),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(7)));

        RequestSession invited = await RegisteringAsync(("clientId", "web"), ("invitationToken", token.Value));
        RequestSession uninvited = await RegisteringAsync(("clientId", "web"));

        _deployment.Configuration.Unread = Settings.PolicyDefault.Key;

        Assert.StartsWith(
            "The policy of the invitation's organization is unreadable",
            await UnreadableAsync(invited),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "The key " + Settings.PolicyDefault.Key.ToString() + " is unreadable",
            await UnreadableAsync(uninvited),
            StringComparison.Ordinal);
    }

    // A browser that opened a registration, as the profile resolves what it carries.
    private async Task<RequestSession> RegisteringAsync(params (string Name, object? Value)[] fields)
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        RegistrationSessionId[] before = [.. _deployment.Registrations.All.Select(session => session.Id)];

        Assert.Equal(StatusCodes.Status201Created, (await browser.SendAsync("POST", "/register", fields)).Status);

        using var randomness = RandomNumberGenerator.Create();
        var resolved = new RequestSession();

        resolved.Resolved(
            _deployment.Contacts.All.Single(contact => contact.Registration is RegistrationSessionId opened
                && !before.Contains(opened)),
            OpaqueToken.Draw(randomness));

        return resolved;
    }

    // What the fault reading the settings says.
    private async Task<string> UnreadableAsync(RequestSession browser)
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        return (await Assert.ThrowsAsync<InvalidOperationException>(async () => await scope.ServiceProvider
                .GetRequiredService<FrontendSettings>()
                .ReadAsync(browser, CancellationToken.None)))
            .Message;
    }
}
