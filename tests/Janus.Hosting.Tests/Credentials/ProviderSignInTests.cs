using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// A sign-in, a registration and a link at a social provider as a browser makes them:
/// the start, the provider's return by a posted form, the continuation that trades the
/// code on the server's own connection, and linking and unlinking an identity
/// (IDN-LIFE-012, IDN-ACCT-001, REG-IDENT-008, BFF-CSRF-005a, BFF-MACH-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProviderSignInTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Page = "/account/profile";
    private const string GoogleSubject = "110169484474386276334";
    private const string AppleSubject = "001234.5a6b7c8d9e0f.1234";
    private const string Gmail = "person@gmail.com";
    private const string Relay = "x7k2mq9p4r@privaterelay.appleid.com";

    private static readonly OrganizationId Branch =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private readonly Deployment _deployment = new();

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that can send the codes and the notices a sign-in and a link
    /// carry.
    /// </summary>
    public ProviderSignInTests()
    {
        Flow.Prepare(_deployment);

        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        foreach (MessageKind message in new[]
            { MessageKind.SecurityNotice, MessageKind.CredentialEnrolled })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "notice" : null, "body"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-LIFE-012 and REG-IDENT-008: an identity linked to an account signs it in over
    /// the round trip, and the browser is sent out with the client, the registered
    /// return, a state, a nonce and a proof key, and asked to come back by a form.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_ALinkedIdentitySignsInOverTheRoundTripAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        var browser = new Browser(_deployment);
        Answer started = await browser.SendAsync("GET", Start("google", "signin"));
        string authorization = Where(started);

        Assert.StartsWith(SocialProvidersInMemory.GoogleAuthorization + "?", authorization, StringComparison.Ordinal);
        Assert.Equal("code", Parameter(authorization, "response_type"));
        Assert.Equal(SocialProvidersInMemory.GoogleClient, Parameter(authorization, "client_id"));
        Assert.Equal(
            _deployment.SocialProviders.Google.Return.AbsoluteUri,
            Parameter(authorization, "redirect_uri"));
        Assert.Equal("openid email", Parameter(authorization, "scope"));
        Assert.Equal("form_post", Parameter(authorization, "response_mode"));
        Assert.Equal("S256", Parameter(authorization, "code_challenge_method"));
        Assert.NotNull(Parameter(authorization, "state"));
        Assert.NotNull(Parameter(authorization, "nonce"));

        Answer landed = await ReturnedAsync(browser, "google", authorization, new ProviderPerson(GoogleSubject));

        Assert.Equal(StatusCodes.Status302Found, landed.Status);
        Assert.Equal(Page, landed.Location);
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("GET", "/auth/session")).Status);

        IReadOnlyDictionary<string, string> exchange = Assert.Single(_deployment.SocialProviders.Exchanges);

        Assert.Equal("the-google-client-secret", exchange["client_secret"]);
        Assert.True(exchange.ContainsKey("code_verifier"));
    }

    /// <summary>
    /// IDN-LIFE-012: a provider whose discovery document lists no S256 proof key is
    /// sent none and is presented none at the exchange.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AProviderThatTakesNoProofKeyIsSentNoneAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Apple, AppleSubject);

        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("apple", "signin")));

        Assert.Null(Parameter(authorization, "code_challenge"));

        Answer landed = await ReturnedAsync(browser, "apple", authorization, new ProviderPerson(AppleSubject));

        Assert.Equal(Page, landed.Location);
        Assert.False(Assert.Single(_deployment.SocialProviders.Exchanges).ContainsKey("code_verifier"));
        Assert.Equal(
            "the-apple-signed-secret",
            Assert.Single(_deployment.SocialProviders.Exchanges)["client_secret"]);
    }

    /// <summary>
    /// IDN-ACCT-001 and IDN-LIFE-012: an identity linked to no account signs nobody in
    /// and creates nothing; the browser is sent back with the code of the refusal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_001_AnIdentityLinkedToNoAccountSignsNobodyInAsync()
    {
        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));

        Answer landed = await ReturnedAsync(
            browser,
            "google",
            authorization,
            new ProviderPerson("a-subject-nobody-linked", Gmail, true));

        Assert.Equal(Page + "?error=" + ErrorCodes.FactorRejected, landed.Location);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await browser.SendAsync("GET", "/auth/session")).Status);
        Assert.Empty(_deployment.Directory.Created);
    }

    /// <summary>
    /// IDN-LIFE-012 and REG-IDENT-008: an identity token is believed only where it holds
    /// up: the nonce this browser was sent with, the deployment's own client, a lifetime
    /// that has not ended and the provider's own key.
    /// </summary>
    /// <param name="wrong">What is wrong with the token.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("nonce")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("forged")]
    public async Task IDN_LIFE_012_AnIdentityTokenThatDoesNotHoldUpSignsNobodyInAsync(string wrong)
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        var person = new ProviderPerson(GoogleSubject);
        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));

        Answer landed = await ReturnedAsync(
            browser,
            "google",
            authorization,
            wrong switch
            {
                "nonce" => person with { Nonce = "a-nonce-this-browser-was-not-sent" },
                "audience" => person with { Audience = "another-client.apps.google.test" },
                "expired" => person with { Expired = true },
                _ => person with { Forged = true },
            });

        Assert.Equal(Page + "?error=" + ErrorCodes.FactorRejected, landed.Location);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await browser.SendAsync("GET", "/auth/session")).Status);
        Assert.Contains((LogLevel.Warning, 20), _deployment.ProviderLog.Entries);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a delegated sign-in refused because the identity is linked to
    /// no account is recorded as a refused factor against no account, with the log
    /// writing nothing at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_005_AC1_AnIdentityLinkedToNoAccountIsRecordedAsync()
    {
        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));

        _ = await ReturnedAsync(
            browser,
            "google",
            authorization,
            new ProviderPerson("a-subject-nobody-linked", Gmail, true));

        Assert.Equal<(SubjectId?, Factor)>([(null, Factor.Google)], _deployment.SessionAudit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1 and AUTH-ABUSE-001: an identity token that does not hold up is
    /// recorded as a refused factor against no account and counted against the
    /// address it came back from; once that address has earned a delay the browser is
    /// sent back throttled and nothing more is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_005_AC1_AForgedIdentityIsRecordedBehindTheDelayAsync()
    {
        var browser = new Browser(_deployment);
        var forged = new ProviderPerson(GoogleSubject) { Forged = true };
        var landed = new List<string?>();

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));

            landed.Add((await ReturnedAsync(browser, "google", authorization, forged)).Location);
        }

        Assert.Equal(
            [
                Page + "?error=" + ErrorCodes.FactorRejected,
                Page + "?error=" + ErrorCodes.FactorRejected,
                Page + "?error=" + ErrorCodes.FactorRejected,
                Page + "?error=" + ErrorCodes.Throttled,
            ],
            landed);
        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.Google), (null, Factor.Google), (null, Factor.Google)],
            _deployment.SessionAudit.Failed);
    }

    /// <summary>
    /// BFF-CSRF-005a: a return presenting a state this browser was not sent out with
    /// establishes nothing and is recorded, and the code is never exchanged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AProviderReturnWithAnotherStateIsRefusedAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));
        string code = _deployment.SocialProviders.Issue(Factor.Google, authorization, new ProviderPerson(GoogleSubject));

        Answer refused = await browser.SendAsync(
            "GET",
            "/auth/providers/google/return?code=" + Uri.EscapeDataString(code) + "&state=a-state-of-another-browser");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.SessionCsrfInvalid.ToString(), refused.Text("code"));
        Assert.Contains((LogLevel.Warning, 18), _deployment.ProviderLog.Entries);
        Assert.Empty(_deployment.SocialProviders.Exchanges);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await browser.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// BFF-CSRF-005a: a round trip is judged once, so the same return presented again
    /// finds nothing bound, and a return reaching a browser that started nothing is
    /// refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AProviderReturnIsJudgedOnceAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync("GET", Start("google", "signin")));
        string code = _deployment.SocialProviders.Issue(Factor.Google, authorization, new ProviderPerson(GoogleSubject));
        string continuation = "/auth/providers/google/return?code=" + Uri.EscapeDataString(code)
            + "&state=" + Uri.EscapeDataString(Parameter(authorization, "state")!);

        var elsewhere = new Browser(_deployment);

        Assert.Equal(StatusCodes.Status403Forbidden, (await elsewhere.SendAsync("GET", continuation)).Status);
        Assert.Equal(Page, (await browser.SendAsync("GET", continuation)).Location);

        browser.Forget();

        Assert.Equal(StatusCodes.Status403Forbidden, (await browser.SendAsync("GET", continuation)).Status);
        Assert.Contains((LogLevel.Warning, 17), _deployment.ProviderLog.Entries);
    }

    /// <summary>
    /// BFF-MACH-001: the return a provider sends the browser to reads nothing of the
    /// browser, refuses no cookie it carries, and sends it on by a read to the
    /// continuation with what the provider answered and nothing else.
    /// </summary>
    /// <param name="method">How the provider returns the browser.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    public async Task BFF_MACH_001_TheProviderReturnSendsTheBrowserOnAsync(string method)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer forwarded = method is "POST"
            ? await browser.SendAsync(
                "POST",
                "/callbacks/providers/apple/return",
                "code=a-code&state=a-state&user=%7B%7D",
                header: false,
                origin: "https://appleid.apple.test",
                token: false,
                contentType: "application/x-www-form-urlencoded")
            : await browser.SendAsync(
                "GET",
                "/callbacks/providers/apple/return?code=a-code&state=a-state&user=x",
                header: false,
                origin: null,
                token: false);

        Assert.Equal(StatusCodes.Status303SeeOther, forwarded.Status);
        Assert.Equal("/auth/providers/apple/return?code=a-code&state=a-state", forwarded.Location);
        Assert.Empty(forwarded.SetCookie);
    }

    /// <summary>
    /// IDN-LIFE-012: the browser comes back onto this application and nowhere else, so
    /// a return address naming another site becomes the root.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AReturnAddressOffThisApplicationIsNotFollowedAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        var browser = new Browser(_deployment);
        string authorization = Where(await browser.SendAsync(
            "GET",
            "/auth/providers/google?intent=signin&returnTo=" + Uri.EscapeDataString("//attacker.test/collect")));

        Answer landed = await ReturnedAsync(browser, "google", authorization, new ProviderPerson(GoogleSubject));

        Assert.Equal("/", landed.Location);
    }

    /// <summary>
    /// IDN-LIFE-012: a provider whose documents cannot be read is not started, and the
    /// browser is sent back with the code of the refusal rather than to the provider.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AProviderThatCannotBeReadIsNotStartedAsync()
    {
        _deployment.SocialProviders.Reachable = false;

        Answer refused = await new Browser(_deployment).SendAsync("GET", Start("google", "signin"));

        Assert.Equal(Page + "?error=" + ErrorCodes.FactorNotPermitted, refused.Location);
        Assert.Contains((LogLevel.Error, 21), _deployment.ProviderLog.Entries);
        Assert.Equal(0, _deployment.ProviderAttempts.Count);
    }

    /// <summary>
    /// REG-IDENT-008 AC1: Continue with Google on a gmail.com address, over the round
    /// trip, reaches the confirm step with the address verified and no code sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_008_AC1_ContinueWithGoogleVerifiesAGmailAddressAsync()
    {
        _deployment.Configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        Browser browser = await AgedAsync();
        Answer landed = await ProvidedAsync(browser, "google", new ProviderPerson(GoogleSubject, Gmail, true));

        Assert.Equal("/register", landed.Location);

        Answer state = await browser.SendAsync("GET", "/register");
        JsonElement email = state.Json().GetProperty("identifiers")[0];

        Assert.Equal(Gmail, email.GetProperty("value").GetString());
        Assert.True(email.GetProperty("verified").GetBoolean());
        Assert.True(email.GetProperty("locked").GetBoolean());
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("POST", "/register/phone/skip")).Status);
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("POST", "/register/confirm")).Status);
        Assert.Empty(_deployment.Mail.Taken);
    }

    /// <summary>
    /// REG-IDENT-008: Apple states whether it verified the address as a string, and a
    /// relay address it verified is verified by the sign-in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_008_AnAppleRelayAddressIsVerifiedOverTheRoundTripAsync()
    {
        Browser browser = await AgedAsync();

        _ = await ProvidedAsync(browser, "apple", new ProviderPerson(AppleSubject, Relay, "true"));

        JsonElement email = (await browser.SendAsync("GET", "/register")).Json().GetProperty("identifiers")[0];

        Assert.True(email.GetProperty("verified").GetBoolean());
        Assert.Empty(_deployment.Mail.Taken);
    }

    /// <summary>
    /// REG-IDENT-008 AC2: Continue with Apple on a third-party address, over the round
    /// trip, sends one code and leaves the address unverified until it is typed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_008_AC2_AThirdPartyAddressFromAppleIsSentOneCodeAsync()
    {
        Browser browser = await AgedAsync();

        _ = await ProvidedAsync(browser, "apple", new ProviderPerson(AppleSubject, Flow.Address, "true"));

        JsonElement email = (await browser.SendAsync("GET", "/register")).Json().GetProperty("identifiers")[0];

        Assert.False(email.GetProperty("verified").GetBoolean());
        _ = Assert.Single(_deployment.Mail.Taken);

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        email = (await browser.SendAsync("GET", "/register")).Json().GetProperty("identifiers")[0];

        Assert.True(email.GetProperty("verified").GetBoolean());
    }

    /// <summary>
    /// REG-IDENT-008 AC3: an identity already linked, met on the registration, signs the
    /// person in and returns them, and leaves no registration session behind.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_008_AC3_ALinkedIdentityOnTheRegistrationSignsInAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await LinkedAsync(subject, Factor.Google, GoogleSubject);

        Browser browser = await AgedAsync();
        Answer landed = await ProvidedAsync(browser, "google", new ProviderPerson(GoogleSubject, Gmail, true));

        Assert.Equal("/register", landed.Location);
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("GET", "/auth/session")).Status);
        Assert.Empty(_deployment.Registrations.All);
    }

    /// <summary>
    /// REG-IDENT-008 AC4: an address another account holds, over the round trip, is
    /// answered as a fresh one is: the same forwarding and the same state, no code, and
    /// the holder told.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_008_AC4_AnAddressAnotherAccountHoldsAnswersAsAFreshOneAsync()
    {
        Browser fresh = await AgedAsync();
        Answer freshLanded = await ProvidedAsync(fresh, "apple", new ProviderPerson(AppleSubject, Flow.Address, "true"));
        Answer freshState = await fresh.SendAsync("GET", "/register");

        _deployment.Directory.Held(IdentifierKind.Email, Flow.Address, SubjectId.New(_randomness));
        int sent = _deployment.Mail.Taken.Count;

        Browser duplicate = await AgedAsync();
        Answer duplicateLanded = await ProvidedAsync(duplicate, "apple", new ProviderPerson("another-subject", Flow.Address, "true"));
        Answer duplicateState = await duplicate.SendAsync("GET", "/register");

        Assert.Equal(freshLanded.Status, duplicateLanded.Status);
        Assert.Equal(freshLanded.Location, duplicateLanded.Location);

        JsonElement one = freshState.Json().GetProperty("identifiers")[0];
        JsonElement two = duplicateState.Json().GetProperty("identifiers")[0];

        Assert.Equal(one.GetProperty("value").GetString(), two.GetProperty("value").GetString());
        Assert.Equal(one.GetProperty("verified").GetBoolean(), two.GetProperty("verified").GetBoolean());
        Assert.Equal(one.GetProperty("locked").GetBoolean(), two.GetProperty("locked").GetBoolean());
        Assert.Equal(freshState.Text("step"), duplicateState.Text("step"));
        Assert.Equal(sent + 1, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// IDN-LIFE-012 AC1: linking an identity attaches a credential, and changes no
    /// grant and no membership the account holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AC1_LinkingChangesNoGrantAndNoMembershipAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        Grant held = await GrantedAsync(subject);

        _deployment.Memberships.Place(subject, Branch);

        int authenticators = _deployment.Authenticators.All.Count;

        Answer linked = await LinkOverTheRoundTripAsync(browser, GoogleSubject);

        Assert.Equal(Page, linked.Location);
        Assert.Equal(authenticators + 1, _deployment.Authenticators.All.Count);
        Assert.Equal(
            subject,
            (await _deployment.Authenticators.ByProviderAsync(Factor.Google, GoogleSubject, CancellationToken.None))!.Subject);
        Assert.Equal([subject], _deployment.Memberships.Members(Branch));
        Assert.Equal(
            [held],
            await _deployment.AccessGrants.HeldByAsync(
                [GrantSubject.Of(subject)],
                Branch,
                _deployment.Clock.GetUtcNow(),
                CancellationToken.None));
    }

    /// <summary>
    /// IDN-LIFE-012 AC2: unlinking leaves the account as it was, and it is signed in to
    /// by what it keeps; the identity unlinked signs nobody in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AC2_UnlinkingLeavesTheAccountUsableByWhatItKeepsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await LinkOverTheRoundTripAsync(browser, GoogleSubject);

        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("DELETE", "/account/link/google")).Status);
        Assert.Null(await _deployment.Authenticators.ByProviderAsync(Factor.Google, GoogleSubject, CancellationToken.None));

        await SignsInWithThePasswordAsync();

        var signing = new Browser(_deployment);
        string authorization = Where(await signing.SendAsync("GET", Start("google", "signin")));

        Assert.Equal(
            Page + "?error=" + ErrorCodes.FactorRejected,
            (await ReturnedAsync(signing, "google", authorization, new ProviderPerson(GoogleSubject))).Location);
    }

    /// <summary>
    /// IDN-ACCT-001 AC1: deleting a linked credential through the credential list leaves
    /// the account intact and signed in to by its other credentials.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_001_AC1_DeletingTheLinkedCredentialLeavesTheAccountUsableAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _ = await LinkOverTheRoundTripAsync(browser, GoogleSubject);

        Authenticator linked = (await _deployment.Authenticators.ByProviderAsync(
            Factor.Google,
            GoogleSubject,
            CancellationToken.None))!;

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("DELETE", "/account/credentials/" + linked.Id)).Status);
        Assert.Equal(AccountState.Active, await _deployment.Accounts.StateAsync(subject, CancellationToken.None));

        await SignsInWithThePasswordAsync();
    }

    /// <summary>
    /// IDN-LIFE-012 AC3: unlinking the only credential the account could be signed in to
    /// by is refused, and the identity stays linked. The credential route asks first
    /// for the step-up a session the provider alone signed in can never pass.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_AC3_UnlinkingTheOnlyRemainingCredentialIsRefusedAsync()
    {
        _deployment.Configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        Browser browser = await AgedAsync();

        _ = await ProvidedAsync(browser, "google", new ProviderPerson(GoogleSubject, Gmail, true));
        _ = await browser.SendAsync("POST", "/register/phone/skip");
        _ = await browser.SendAsync("POST", "/register/confirm");

        Assert.Equal(
            StatusCodes.Status201Created,
            (await browser.SendAsync(
                "POST",
                "/register/terms",
                ("termsVersion", "terms-3"),
                ("noticeVersion", "notice-2"))).Status);

        Flow.Carried(_deployment);

        Authenticator linked = (await _deployment.Authenticators.ByProviderAsync(
            Factor.Google,
            GoogleSubject,
            CancellationToken.None))!;

        Answer unlinked = await browser.SendAsync("DELETE", "/account/link/google");
        Answer removed = await browser.SendAsync("DELETE", "/account/credentials/" + linked.Id);

        Assert.Equal(StatusCodes.Status409Conflict, unlinked.Status);
        Assert.Equal(ErrorCodes.LinkLastCredential.ToString(), unlinked.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, removed.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), removed.Text("code"));
        Assert.NotNull(await _deployment.Authenticators.FindAsync(linked.Id, CancellationToken.None));
    }

    /// <summary>
    /// IDN-LIFE-012 and chapter 10 section 5a: linking asks for the step-up its gate
    /// declares, both where the browser asks whether it may and where it starts the
    /// round trip.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_LinkingAsksForTheStepUpItsGateDeclaresAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer begun = await browser.SendAsync(
            "POST",
            "/account/factors/totp/begin",
            ("label", "This phone"));

        _ = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(begun.Text("secret"))).ComputeTotp(
                _deployment.Clock.GetUtcNow().UtcDateTime)));

        Answer asked = await browser.SendAsync("POST", "/account/link/google");
        Answer started = await browser.SendAsync("GET", Start("google", "link"));

        Assert.Equal(StatusCodes.Status403Forbidden, asked.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), asked.Text("code"));
        Assert.Equal(Page + "?error=" + ErrorCodes.StepUpRequired, started.Location);
        Assert.Equal(0, _deployment.ProviderAttempts.Count);
    }

    /// <summary>
    /// IDN-LIFE-012: unlinking a provider the account holds no identity at is answered
    /// as the absence it is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012_UnlinkingWhatIsNotLinkedFindsNothingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("DELETE", "/account/link/apple");

        Assert.Equal(StatusCodes.Status404NotFound, refused.Status);
        Assert.Equal(ErrorCodes.CredentialNotFound.ToString(), refused.Text("code"));
    }

    private static string Start(string provider, string intent) =>
        "/auth/providers/" + provider + "?intent=" + intent + "&returnTo=" + Uri.EscapeDataString(Page);

    private static string Where(Answer answered) =>
        answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere.");

    private static string? Parameter(string where, string name) =>
        SocialProvidersInMemory.Parameter(where, name);

    // The provider signs the person in, and returns the browser by a posted form to the
    // return, which sends it on to the continuation.
    private async Task<Answer> ReturnedAsync(
        Browser browser,
        string provider,
        string authorization,
        ProviderPerson person)
    {
        string code = _deployment.SocialProviders.Issue(
            ProviderRoutesUnderTest(provider),
            authorization,
            person);

        Answer forwarded = await browser.SendAsync(
            "POST",
            "/callbacks/providers/" + provider + "/return",
            "code=" + Uri.EscapeDataString(code) + "&state=" + Uri.EscapeDataString(Parameter(authorization, "state")!),
            header: false,
            origin: null,
            token: false,
            contentType: "application/x-www-form-urlencoded");

        Assert.Equal(StatusCodes.Status303SeeOther, forwarded.Status);

        return await browser.SendAsync("GET", Where(forwarded));
    }

    private static Factor ProviderRoutesUnderTest(string provider) =>
        provider is "google" ? Factor.Google : Factor.Apple;

    // A browser at the email step of a registration: begun, and the age answered.
    private async Task<Browser> AgedAsync()
    {
        Browser browser = await Flow.BegunAsync(_deployment);

        _ = await browser.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));

        return browser;
    }

    // Continue with a provider at the email step, and back to the registration.
    private async Task<Answer> ProvidedAsync(Browser browser, string provider, ProviderPerson person)
    {
        string authorization = Where(await browser.SendAsync(
            "GET",
            "/auth/providers/" + provider + "?intent=register&returnTo=%2Fregister"));

        return await ReturnedAsync(browser, provider, authorization, person);
    }

    private async Task<Answer> LinkOverTheRoundTripAsync(Browser browser, string providerSubject)
    {
        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("POST", "/account/link/google")).Status);

        string authorization = Where(await browser.SendAsync("GET", Start("google", "link")));

        return await ReturnedAsync(browser, "google", authorization, new ProviderPerson(providerSubject));
    }

    // An account a registration created and carried across, which a provider's identity
    // can then be linked to.
    private async Task<SubjectId> RegisteredAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        return _deployment.Directory.Created[^1].Subject;
    }

    private async Task LinkedAsync(SubjectId subject, Factor provider, string providerSubject) =>
        await _deployment.Authenticators.LinkAsync(
            Authenticator.Linked(
                AuthenticatorId.New(_deployment.Clock),
                subject,
                provider,
                CredentialLabel.Of(new DeviceDescription("Firefox", "Fedora")),
                _deployment.Clock.GetUtcNow()),
            providerSubject,
            CancellationToken.None);

    private async Task SignsInWithThePasswordAsync()
    {
        var signing = new Browser(_deployment);

        _ = await signing.SendAsync("GET", "/auth/session");

        Answer begun = await signing.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer reached = await signing.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", begun.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal(StatusCodes.Status200OK, reached.Status);
        Assert.Equal("complete", reached.Text("status"));
    }

    private async Task<Grant> GrantedAsync(SubjectId subject)
    {
        Grant grant = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(subject),
                RoleName.Parse("editor"),
                Branch,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                subject,
                _deployment.Clock.GetUtcNow(),
                "Edits the branch.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(grant, CancellationToken.None);

        return grant;
    }
}
