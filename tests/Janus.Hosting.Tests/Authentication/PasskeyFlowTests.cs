using System;
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
/// The two WebAuthn ceremonies as a browser drives them: the creation's answer in
/// <c>credential</c> and the assertion as the <c>value</c> of the factor, each as
/// <c>PublicKeyCredential.toJSON()</c> gives it (chapter 09 sections 3 and 4,
/// AUTH-FACT-014).
/// </summary>
[Trait("kind", "unit")]
public sealed class PasskeyFlowTests : IAsyncDisposable
{
    private const string Session = "__Host-identity-session";
    private const string Origin = "https://identity.example.test";
    private const string RelyingPartyId = "identity.example.test";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose messages carry the code and the link token, as the shipped
    /// templates do.
    /// </summary>
    public PasskeyFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-FACT-014 AC6: a passkey whose authenticator chooses EdDSA enrols from the
    /// <c>RegistrationResponseJSON</c> in <c>credential</c>, its key and transports kept
    /// as the response gives them, and signs in with the
    /// <c>AuthenticationResponseJSON</c> as the <c>value</c>, the counter of the
    /// assertion stored. The members a browser adds that the library does not read,
    /// the client extension outputs among them, change nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC6_APasskeyOfEdDsaEnrolsAndSignsInOverTheWireAsync()
    {
        var authenticator = SoftwareAuthenticator.Holding(SoftwareAuthenticator.EdDsa);

        Answer created = await RegisteredAsync(authenticator);

        Authenticator held = Assert.Single(_deployment.Authenticators.All);

        Assert.Equal(StatusCodes.Status200OK, created.Status);
        Assert.Equal(SoftwareAuthenticator.EdDsa, held.WebAuthn!.Algorithm);
        Assert.Equal(authenticator.PublicKey, held.WebAuthn.PublicKey.ToArray());
        Assert.Equal(["internal", "hybrid"], held.WebAuthn.Transports);

        var browser = new Browser(_deployment);
        (string challenge, string signed) = await BegunAsync(browser);

        Answer answered = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, authenticator.Asserted(
                signed,
                Origin,
                RelyingPartyId,
                counter: 7,
                userHandle: WebAuthnService.Handle(held.Subject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("complete", answered.Text("status"));
        Assert.True(browser.Cookies.ContainsKey(Session));
        Assert.Equal(7u, Assert.Single(_deployment.Authenticators.All).WebAuthn!.Counter);
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: a creation whose client data's <c>crossOrigin</c> is true is
    /// refused 422 <c>auth.factor.rejected</c> and nothing is staged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC8_ACreationWhoseClientDataIsCrossOriginIsRefusedOverTheWireAsync()
    {
        Browser registering = await Flow.ConfirmedAsync(_deployment);
        Answer begun = await registering.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey"));

        Answer embedded = await registering.SendAsync(
            "POST",
            "/auth/webauthn/register/complete",
            Created(SoftwareAuthenticator.Holding().Attested(
                begun.Text("challenge"),
                Origin,
                RelyingPartyId,
                crossOrigin: true)));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, embedded.Status);
        Assert.Equal(ErrorCodes.FactorRejected.ToString(), embedded.Text("code"));
        Assert.Equal("security", (await registering.SendAsync("GET", "/register")).Text("step"));
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: an assertion whose client data's <c>crossOrigin</c> is true is
    /// refused 422 <c>auth.factor.rejected</c>, the browser left holding no session and
    /// nothing of the assertion stored.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC8_AnAssertionWhoseClientDataIsCrossOriginIsRefusedOverTheWireAsync()
    {
        var authenticator = SoftwareAuthenticator.Holding();

        _ = await RegisteredAsync(authenticator);

        Authenticator held = Assert.Single(_deployment.Authenticators.All);
        var browser = new Browser(_deployment);
        (string challenge, string signed) = await BegunAsync(browser);

        Answer refused = await browser.SendAsync(
            "POST",
            "/auth/factor",
            Presented(challenge, authenticator.Asserted(
                signed,
                Origin,
                RelyingPartyId,
                counter: 7,
                crossOrigin: true,
                userHandle: WebAuthnService.Handle(held.Subject))));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.FactorRejected.ToString(), refused.Text("code"));
        Assert.False(browser.Cookies.ContainsKey(Session));
        Assert.Null(Assert.Single(_deployment.Authenticators.All).WebAuthn!.Counter);
    }

    /// <summary>
    /// AUTH-FACT-014, API section 3: a <c>value</c> that is neither text nor an
    /// <c>AuthenticationResponseJSON</c> that reads (a member the ceremony needs is
    /// absent, is not text or is not base64url) is 400 <c>api.request.malformed</c>
    /// naming <c>value</c>, and no factor is judged.
    /// </summary>
    /// <param name="fault">What is wrong with the value.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("number")]
    [InlineData("array")]
    [InlineData("empty")]
    [InlineData("noResponse")]
    [InlineData("noSignature")]
    [InlineData("identifier")]
    [InlineData("encoding")]
    [InlineData("userHandle")]
    public async Task AUTH_FACT_014_AValueThatIsNoAuthenticationResponseIsMalformedAsync(string fault)
    {
        var authenticator = SoftwareAuthenticator.Holding();

        _ = await RegisteredAsync(authenticator);

        var browser = new Browser(_deployment);
        (string challenge, string signed) = await BegunAsync(browser);
        JsonObject sound = SoftwareAuthenticator.Written(authenticator.Asserted(signed, Origin, RelyingPartyId));
        JsonObject response = sound["response"]!.AsObject();
        JsonNode value = sound;

        switch (fault)
        {
            case "number":
                value = JsonValue.Create(123456);
                break;
            case "array":
                value = new JsonArray("123456");
                break;
            case "empty":
                value = new JsonObject();
                break;
            case "noResponse":
                _ = sound.Remove("response");
                break;
            case "noSignature":
                _ = response.Remove("signature");
                break;
            case "identifier":
                sound["rawId"] = 7;
                break;
            case "encoding":
                response["authenticatorData"] = "not base64url!";
                break;
            default:
                response["userHandle"] = "not base64url!";
                break;
        }

        Answer refused = await browser.SendAsync(
            "POST",
            "/auth/factor",
            new JsonObject
            {
                ["challengeId"] = challenge,
                ["factor"] = "passkey",
                ["value"] = value,
            }.ToJsonString());

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.Equal("value", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.False(browser.Cookies.ContainsKey(Session));
        Assert.Null(Assert.Single(_deployment.Authenticators.All).LastUsedAt);
    }

    /// <summary>
    /// AUTH-FACT-014, API section 4: a <c>credential</c> that is no
    /// <c>RegistrationResponseJSON</c> that reads (the members of a shape the endpoint
    /// took before, a member the ceremony needs absent or not base64url) is 400
    /// <c>api.request.malformed</c> naming <c>credential</c>, and nothing is staged.
    /// </summary>
    /// <param name="fault">What is wrong with the credential.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("absent")]
    [InlineData("flat")]
    [InlineData("noResponse")]
    [InlineData("noAttestationObject")]
    [InlineData("encoding")]
    public async Task AUTH_FACT_014_ACredentialThatIsNoRegistrationResponseIsMalformedAsync(string fault)
    {
        Browser browser = await Flow.ConfirmedAsync(_deployment);
        Answer begun = await browser.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey"));
        AuthenticatorAttestation attested = SoftwareAuthenticator.Holding()
            .Attested(begun.Text("challenge"), Origin, RelyingPartyId);
        JsonObject sound = SoftwareAuthenticator.Written(attested);
        JsonObject response = sound["response"]!.AsObject();
        JsonNode? credential = sound;

        switch (fault)
        {
            case "absent":
                credential = null;
                break;
            case "flat":
                credential = new JsonObject
                {
                    ["credentialId"] = attested.RawId,
                    ["clientDataJson"] = attested.ClientDataJson,
                    ["authenticatorData"] = attested.AttestationObject,
                    ["publicKey"] = attested.RawId,
                    ["algorithm"] = -7,
                };
                break;
            case "noResponse":
                _ = sound.Remove("response");
                break;
            case "noAttestationObject":
                _ = response.Remove("attestationObject");
                break;
            default:
                response["clientDataJSON"] = "not base64url!";
                break;
        }

        Answer refused = await browser.SendAsync(
            "POST",
            "/auth/webauthn/register/complete",
            new JsonObject { ["credential"] = credential, ["label"] = "This phone" }.ToJsonString());

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.Equal("credential", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal("security", (await browser.SendAsync("GET", "/register")).Text("step"));
    }

    private static string Created(AuthenticatorAttestation attested) =>
        new JsonObject
        {
            ["credential"] = SoftwareAuthenticator.Written(attested),
            ["label"] = "This phone",
        }.ToJsonString();

    private static string Presented(string challenge, AuthenticatorAssertion asserted) =>
        new JsonObject
        {
            ["challengeId"] = challenge,
            ["factor"] = "passkey",
            ["value"] = SoftwareAuthenticator.Written(asserted),
        }.ToJsonString();

    // A browser that has a first contact and a sign-in open against the address the
    // registration flow registered, with the challenge its ceremony signs.
    private static async Task<(string Challenge, string Signed)> BegunAsync(Browser browser)
    {
        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return (
            began.Text("challengeId"),
            began.Json().GetProperty("webAuthn").GetProperty("challenge").GetString()!);
    }

    // An account registered with the authenticator's passkey and nothing else, the
    // new-device check off so that the passkey's own answer is what a sign-in gets.
    private async Task<Answer> RegisteredAsync(SoftwareAuthenticator authenticator)
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        Browser browser = await Flow.ConfirmedAsync(_deployment);
        Answer begun = await browser.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey"));

        Answer created = await browser.SendAsync(
            "POST",
            "/auth/webauthn/register/complete",
            Created(authenticator.Attested(
                begun.Text("challenge"),
                Origin,
                RelyingPartyId,
                (byte)(SoftwareAuthenticator.UserPresent | SoftwareAuthenticator.UserVerified),
                transports: ["internal", "hybrid"])));

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));

        Assert.Equal(StatusCodes.Status201Created, completed.Status);

        Flow.Carried(_deployment);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return created;
    }
}
