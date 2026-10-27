using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Tests.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authentication;

/// <summary>
/// A security key as a second step, as a browser drives it: the answer that offers it
/// carries the request options that name it, and the assertion made from them is the
/// next call's <c>value</c> (chapter 09 <c>POST /auth/factor</c> and
/// <c>POST /auth/step-up</c>, AUTH-FACT-002b).
/// </summary>
[Trait("kind", "unit")]
public sealed class SecurityKeyFlowTests
{
    private const string Session = "__Host-identity-session";
    private const string Origin = "https://identity.example.test";
    private const string RelyingPartyId = "identity.example.test";

    private static readonly Gate PhishingResistant =
        new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

    /// <summary>
    /// AUTH-FACT-002b AC8: <c>POST /auth/begin</c> names no credential, for an account
    /// holding a security key as for an identifier no account holds; once the password
    /// is accepted, the <c>factorRequired</c> answer naming <c>securityKey</c> carries
    /// <c>webauthn</c> with <c>userVerification</c> <c>required</c>, the challenge the
    /// sign-in opened with, and <c>allowCredentials</c> naming exactly the account's
    /// active second-factor security keys with their transports (a suspended one,
    /// another account's and a passkey are not named); the assertion made from those
    /// options completes the sign-in, and the completing answer carries no
    /// <c>webauthn</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AtASignInTheSecurityKeyIsNamedOnceThePasswordIsAcceptedAsync()
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        SubjectId subject = deployment.Directory.Created[^1].Subject;
        var key = SoftwareAuthenticator.Holding();

        deployment.Authenticators.Hold(Held(deployment, subject, Factor.SecurityKey, key, "Key", "usb", "nfc"));
        deployment.Authenticators.Hold(Suspended(
            Held(deployment, subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Lost", "usb"),
            deployment));
        deployment.Authenticators.Hold(
            Held(deployment, subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Phone", "internal"));
        deployment.Authenticators.Hold(Held(
            deployment,
            new SubjectId(Guid.NewGuid()),
            Factor.SecurityKey,
            SoftwareAuthenticator.Holding(),
            "Theirs",
            "usb"));

        var browser = new Browser(deployment);
        var stranger = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");
        _ = await stranger.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer nobody = await stranger.SendAsync("POST", "/auth/begin", ("identifier", "nobody@example.test"));
        string challenge = began.Text("challengeId");
        string signed = began.Json().GetProperty("webauthn").GetProperty("challenge").GetString()!;

        Answer first = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));
        JsonElement options = first.Json().GetProperty("webauthn");

        Answer second = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, "securityKey", key.Asserted(
                options.GetProperty("challenge").GetString()!,
                Origin,
                options.GetProperty("relyingPartyId").GetString()!)));

        Assert.Equal(["challengeId", "available", "webauthn"], Members(began.Json()));
        Assert.Equal(["relyingPartyId", "challenge"], Members(began.Json().GetProperty("webauthn")));
        Assert.Equal(Members(began.Json()), Members(nobody.Json()));
        Assert.Equal(Members(began.Json().GetProperty("webauthn")), Members(nobody.Json().GetProperty("webauthn")));
        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal("factorRequired", first.Text("status"));
        Assert.Equal(["securityKey"], Named(first.Json().GetProperty("required")));
        Assert.Equal(
            ["relyingPartyId", "challenge", "allowCredentials", "userVerification"],
            Members(options));
        Assert.Equal("required", options.GetProperty("userVerification").GetString());
        Assert.Equal(RelyingPartyId, options.GetProperty("relyingPartyId").GetString());
        Assert.Equal(signed, options.GetProperty("challenge").GetString());
        Assert.Equal([(Base64Url.EncodeToString(key.CredentialId), "usb,nfc")], Allowed(options));
        Assert.Equal(StatusCodes.Status200OK, second.Status);
        Assert.Equal("complete", second.Text("status"));
        Assert.Equal("aal2", second.Text("assuranceLevel"));
        Assert.True(second.Json().GetProperty("phishingResistant").GetBoolean());
        Assert.False(second.Json().TryGetProperty("webauthn", out _));
        Assert.True(browser.Cookies.ContainsKey(Session));
    }

    /// <summary>
    /// AUTH-FACT-002b AC8: a <c>factorRequired</c> answer that does not name
    /// <c>securityKey</c> carries no <c>webauthn</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AnAnswerThatDoesNotNameTheSecurityKeyCarriesNoWebAuthnAsync()
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        SubjectId subject = deployment.Directory.Created[^1].Subject;

        deployment.Authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(deployment.Clock),
            subject,
            Factor.Totp,
            Label("Generator"),
            AuthenticatorState.Active,
            deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            new TotpMaterial(new byte[20], null),
            null,
            isPreferred: false));

        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer first = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal("factorRequired", first.Text("status"));
        Assert.Equal(["totp"], Named(first.Json().GetProperty("required")));
        Assert.False(first.Json().TryGetProperty("webauthn", out _));
    }

    /// <summary>
    /// AUTH-FACT-002b AC8, `09` <c>POST /auth/step-up</c>: at a step-up a
    /// <c>factorRequired</c> answer given before any factor has been presented on the
    /// challenge carries no <c>webauthn</c>, though it names <c>securityKey</c>; once
    /// the password has been presented, the answer carries it, and since it offers
    /// <c>passkey</c> too, <c>allowCredentials</c> names the account's active passkeys
    /// beside its security keys; a passkey's assertion made from those options
    /// completes the step-up at <c>aal2</c>, phishing-resistant.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AtAStepUpTheCredentialsAreNamedOnceAFactorHasBeenPresentedAsync()
    {
        await using var deployment = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk)));

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
                Gates = Policies.SystemDefault.Gates.ToDictionary(declared => declared.Key, _ => PhishingResistant),
            });

        Browser owner = await Flow.SignedInAsync(deployment);

        deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        SubjectId subject = deployment.Directory.Created[^1].Subject;
        var key = SoftwareAuthenticator.Holding();
        var passkey = SoftwareAuthenticator.Holding();

        deployment.Authenticators.Hold(Held(deployment, subject, Factor.SecurityKey, key, "Key", "usb"));
        deployment.Authenticators.Hold(
            Held(deployment, subject, Factor.Passkey, passkey, "Phone", "internal", "hybrid"));
        deployment.Authenticators.Hold(Suspended(
            Held(deployment, subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Lost", "internal"),
            deployment));
        deployment.Authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(deployment.Clock),
            subject,
            Factor.PhoneCode,
            Label("Number"),
            AuthenticatorState.Active,
            deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            null,
            isPreferred: false,
            number: Flow.Number));

        Answer began = await owner.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        string challenge = began.Text("challengeId");

        Answer before = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "phoneCode"));
        Answer first = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));
        JsonElement options = first.Json().GetProperty("webauthn");

        Answer second = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            Presented(challenge, "passkey", passkey.Asserted(
                options.GetProperty("challenge").GetString()!,
                Origin,
                options.GetProperty("relyingPartyId").GetString()!,
                userHandle: WebAuthnService.Handle(subject))));

        Assert.Equal(StatusCodes.Status200OK, before.Status);
        Assert.Equal("factorRequired", before.Text("status"));
        Assert.Contains("securityKey", Named(before.Json().GetProperty("required")));
        Assert.False(before.Json().TryGetProperty("webauthn", out _));
        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal("factorRequired", first.Text("status"));
        Assert.Equal(
            ["passkey", "securityKey"],
            Named(first.Json().GetProperty("required")).Order(StringComparer.Ordinal));
        Assert.Equal("required", options.GetProperty("userVerification").GetString());
        Assert.Equal(RelyingPartyId, options.GetProperty("relyingPartyId").GetString());
        Assert.Equal(
            began.Json().GetProperty("webauthn").GetProperty("challenge").GetString(),
            options.GetProperty("challenge").GetString());
        Assert.Equal(
            new[]
            {
                (Base64Url.EncodeToString(key.CredentialId), "usb"),
                (Base64Url.EncodeToString(passkey.CredentialId), "internal,hybrid"),
            }.Order(),
            Allowed(options).Order());
        Assert.Equal(StatusCodes.Status200OK, second.Status);
        Assert.Equal("complete", second.Text("status"));
        Assert.Equal("aal2", second.Text("assuranceLevel"));
        Assert.True(second.Json().GetProperty("phishingResistant").GetBoolean());
        Assert.False(second.Json().TryGetProperty("webauthn", out _));
    }

    /// <summary>
    /// AUTH-FACT-002b AC9, `09` <c>POST /auth/factor</c>: before a first factor, a
    /// security key's assertion presented as <c>passkey</c> is answered 422
    /// <c>auth.factor.notpermitted</c> once it verifies, for an active key, a suspended
    /// one, under a policy whose <c>loginFactors</c> omits <c>passkey</c> and under a
    /// domain lock on the sign-in's address alike, with no failed authentication
    /// recorded, no session set and nothing of the credential written; an assertion
    /// naming the same key that another key signed is answered 422
    /// <c>auth.factor.rejected</c>, a failed authentication recorded.
    /// </summary>
    /// <param name="standing">What stands against the key or the sign-in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("active")]
    [InlineData("suspended")]
    [InlineData("policy")]
    [InlineData("lock")]
    public async Task AUTH_FACT_002b_AC9_ASecurityKeysAssertionPresentedAsPasskeyBeforeAFirstFactorIsAnswered422Async(
        string standing)
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        SubjectId subject = deployment.Directory.Created[^1].Subject;
        var key = SoftwareAuthenticator.Holding();
        var forger = SoftwareAuthenticator.Holding(credentialId: key.CredentialId);
        Authenticator held = Held(deployment, subject, Factor.SecurityKey, key, "Key", "usb");

        deployment.Authenticators.Hold(held);

        switch (standing)
        {
            case "suspended":
                _ = Suspended(held, deployment);
                break;
            case "policy":
                deployment.Configuration.Set(
                    Settings.PolicyDefault,
                    Policies.SystemDefault with
                    {
                        LoginFactors = new HashSet<Factor>(
                            Policies.SystemDefault.LoginFactors.Where(factor => factor != Factor.Passkey)),
                    });
                break;
            case "lock":
                var locked = new OrganizationId(Guid.NewGuid());

                deployment.Memberships.Place(subject, locked);
                deployment.Configuration.Set(
                    Settings.OrganizationPolicy,
                    locked.ToString(),
                    PolicyOverride.None with { EmailDomains = ["elsewhere.test"] });
                break;
            default:
                break;
        }

        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        string challenge = began.Text("challengeId");
        string signed = began.Json().GetProperty("webauthn").GetProperty("challenge").GetString()!;
        int failed = deployment.SessionAudit.Failed.Count;

        Answer verified = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, "passkey", key.Asserted(signed, Origin, RelyingPartyId, counter: 4)));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, verified.Status);
        Assert.Equal("auth.factor.notpermitted", verified.Text("code"));
        Assert.Equal(failed, deployment.SessionAudit.Failed.Count);
        Assert.False(browser.Cookies.ContainsKey(Session));
        Assert.Equal((null, null), (held.WebAuthn?.Counter, held.LastUsedAt));

        Answer forged = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, "passkey", forger.Asserted(signed, Origin, RelyingPartyId, counter: 4)));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, forged.Status);
        Assert.Equal("auth.factor.rejected", forged.Text("code"));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], [.. deployment.SessionAudit.Failed.Skip(failed)]);
    }

    /// <summary>
    /// AUTH-FACT-002b AC10: over <c>POST /auth/factor</c> before a first factor, a
    /// security key's assertion presented as <c>passkey</c> that verifies is answered
    /// 422 <c>auth.factor.rejected</c> where the key is another account's than the one
    /// the sign-in is for, and where the sign-in's identifier names no account, a
    /// failed authentication recorded and no session set.
    /// </summary>
    /// <param name="named">Whether the sign-in's identifier names an account.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_002b_AC10_ASecurityKeyTheSignInsAccountDoesNotHoldIsAnswered422RejectedAsync(bool named)
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        SubjectId subject = deployment.Directory.Created[^1].Subject;
        var key = SoftwareAuthenticator.Holding();

        deployment.Authenticators.Hold(Held(
            deployment,
            named ? new SubjectId(Guid.NewGuid()) : subject,
            Factor.SecurityKey,
            key,
            "Key",
            "usb"));

        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync(
            "POST",
            "/auth/begin",
            ("identifier", named ? Flow.Address : "nobody@example.test"));
        string challenge = began.Text("challengeId");
        string signed = began.Json().GetProperty("webauthn").GetProperty("challenge").GetString()!;
        int failed = deployment.SessionAudit.Failed.Count;

        Answer refused = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, "passkey", key.Asserted(signed, Origin, RelyingPartyId, counter: 4)));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal("auth.factor.rejected", refused.Text("code"));
        Assert.Equal<(SubjectId?, Factor)>(
            [(named ? subject : null, Factor.Passkey)],
            [.. deployment.SessionAudit.Failed.Skip(failed)]);
        Assert.False(browser.Cookies.ContainsKey(Session));
    }

    /// <summary>
    /// AUTH-FACT-014 AC9: over <c>POST /auth/factor</c>, at a sign-in for an account,
    /// an assertion presented as <c>passkey</c> whose credential another account holds
    /// is answered 422 <c>auth.factor.rejected</c> before it is verified, where its
    /// user verification flag is clear and where its counter did not advance alike: a
    /// failed authentication recorded, no <c>auth.credential.countermismatch</c>
    /// recorded, nothing of the credential written, and the same answer a sign-in whose
    /// identifier names no account is given.
    /// </summary>
    /// <param name="factor">The kind of the credential the other account holds.</param>
    /// <param name="failing">What the assertion would fail at its verification.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("passkey", "userVerification")]
    [InlineData("passkey", "counter")]
    [InlineData("securityKey", "userVerification")]
    [InlineData("securityKey", "counter")]
    public async Task AUTH_FACT_014_AC9_ACredentialTheSignInsAccountDoesNotHoldIsAnswered422RejectedAsync(
        string factor,
        string failing)
    {
        await using var deployment = new Deployment();

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        SubjectId subject = deployment.Directory.Created[^1].Subject;
        var key = SoftwareAuthenticator.Holding();
        Authenticator held = Held(
            deployment,
            new SubjectId(Guid.NewGuid()),
            factor is "securityKey" ? Factor.SecurityKey : Factor.Passkey,
            key,
            "Key",
            "usb");

        held.Counted(9);
        deployment.Authenticators.Hold(held);

        int failed = deployment.SessionAudit.Failed.Count;
        int recorded = deployment.CredentialAudit.Records.Count;
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer refused = await AnsweredAsync(Flow.Address);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal("auth.factor.rejected", refused.Text("code"));
        Assert.Equal<(SubjectId?, Factor)>(
            [(subject, Factor.Passkey)],
            [.. deployment.SessionAudit.Failed.Skip(failed)]);
        Assert.Equal(recorded, deployment.CredentialAudit.Records.Count);
        Assert.False(browser.Cookies.ContainsKey(Session));
        Assert.Equal((9u, null), (held.WebAuthn?.Counter, held.LastUsedAt));

        Answer unnamed = await AnsweredAsync("nobody@example.test");

        Assert.Equal(refused.Status, unnamed.Status);
        Assert.Equal(refused.Text("code"), unnamed.Text("code"));
        Assert.Equal(refused.Json().GetProperty("details").GetRawText(), unnamed.Json().GetProperty("details").GetRawText());

        async ValueTask<Answer> AnsweredAsync(string identifier)
        {
            Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", identifier));

            return await browser.SendAsync(
                "POST",
                "/auth/factor",
                Presented(
                    began.Text("challengeId"),
                    "passkey",
                    key.Asserted(
                        began.Json().GetProperty("webauthn").GetProperty("challenge").GetString()!,
                        Origin,
                        RelyingPartyId,
                        (byte)(failing is "userVerification"
                            ? SoftwareAuthenticator.UserPresent
                            : SoftwareAuthenticator.UserPresent | SoftwareAuthenticator.UserVerified),
                        counter: failing is "counter" ? 9u : 10u)));
        }
    }

    private static string Presented(string challenge, string factor, AuthenticatorAssertion asserted) =>
        new JsonObject
        {
            ["challengeId"] = challenge,
            ["factor"] = factor,
            ["value"] = SoftwareAuthenticator.Written(asserted),
        }.ToJsonString();

    // The names an object carries, in the order it carries them.
    private static string[] Members(JsonElement written) =>
        [.. written.EnumerateObject().Select(member => member.Name)];

    private static string[] Named(JsonElement factors) =>
        [.. factors.EnumerateArray().Select(factor => factor.GetString() ?? string.Empty)];

    // The credentials the options name, each as its identifier and its transports, and
    // as carrying those two members alone.
    private static (string Id, string Transports)[] Allowed(JsonElement options) =>
    [
        .. options.GetProperty("allowCredentials").EnumerateArray().Select(named =>
        {
            Assert.Equal(["id", "transports"], Members(named));

            return (
                named.GetProperty("id").GetString() ?? string.Empty,
                string.Join(',', named.GetProperty("transports").EnumerateArray().Select(way => way.GetString())));
        }),
    ];

    private static CredentialLabel Label(string named) =>
        CredentialLabel.TryParse(named, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label does not read.");

    // A WebAuthn credential as the enrolment of the authenticator that holds it left
    // it: its identifier, its key and the transports the browser reported.
    private static Authenticator Held(
        Deployment deployment,
        SubjectId subject,
        Factor factor,
        SoftwareAuthenticator authenticator,
        string named,
        params string[] transports) =>
        Authenticator.Existing(
            AuthenticatorId.New(deployment.Clock),
            subject,
            factor,
            Label(named),
            AuthenticatorState.Active,
            deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            new WebAuthnMaterial(
                authenticator.CredentialId,
                authenticator.PublicKey,
                SoftwareAuthenticator.Es256,
                RelyingPartyId,
                null,
                false,
                false,
                transports),
            isPreferred: false);

    // AUTH-RECOV-007: the credential as a loss report leaves it.
    private static Authenticator Suspended(Authenticator held, Deployment deployment)
    {
        held.Suspend(deployment.Clock.GetUtcNow() + TimeSpan.FromDays(7));

        return held;
    }
}
