using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// The social providers' security events, delivered as Google and Apple deliver them:
/// on the machine profile, signed with the keys each publishes, and carried once
/// (IDN-LIFE-012a).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProviderEventTests : IAsyncDisposable
{
    private const string Language = "en";

    private const string Google = "/callbacks/providers/google";

    private const string Apple = "/callbacks/providers/apple";

    private const string Risc = "https://schemas.openid.net/secevent/risc/event-type/";

    private const string GoogleSubject = "110169484474386276334";

    private const string AppleSubject = "001234.0f7fd2a1c3e24e1b9a5d1c1e8f0e6a4b.0456";

    private const string Delivered = "application/secevent+jwt";

    private readonly Deployment _deployment = new();

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that takes both providers' events and can send the notice a
    /// removed credential or a suspended account carries.
    /// </summary>
    public ProviderEventTests() => Prepared(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();

        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-LIFE-012a AC1: a signed event saying the provider account was compromised,
    /// disabled or signed out everywhere ends every session of the linked account and
    /// holds the credential; the provider is answered as RFC 8935 asks.
    /// </summary>
    /// <param name="type">The event's type.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("sessions-revoked")]
    [InlineData("account-disabled")]
    public async Task IDN_LIFE_012a_AC1_ASignedCompromiseEndsEverySessionAndHoldsTheCredentialAsync(string type)
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);

        Assert.NotEmpty(Live(subject));

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(Factor.Google, "evt-1", GoogleEvent(Risc + type, GoogleSubject)));

        Assert.Equal(StatusCodes.Status202Accepted, answered.Status);
        Assert.Empty(Live(subject));
        Assert.True(Held(linked).IsHeldByProvider);
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, Risc + type, ProviderEventOutcome.SessionsEnded),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC1: an event the provider's published keys do not verify changes
    /// nothing, is refused as RFC 8935 refuses a key that does not verify, and is
    /// audited as rejected against the account whose linked identity it names.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC1_AnUnsignedEventChangesNothingAndIsAuditedAsRejectedAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Forged(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Equal("invalid_key", answered.Text("err"));
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Equal(
            (AuditActions.ProviderEventRejected, linked.Id, Risc + "sessions-revoked", ProviderEventOutcome.Unsigned),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC3, 09 section 10: on the Apple route an event the provider's
    /// published keys do not verify is refused as every rejected callback is, 422 with
    /// no interval, changes nothing and is audited as rejected.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC3_TheAppleRouteRefusesAnUnsignedEventAsARejectedCallbackAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Apple, AppleSubject);
        int live = Live(subject).Count;

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Forged(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, answered.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), answered.Text("code"));
        Assert.Null(answered.Header(HeaderNames.RetryAfter));
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Equal(
            (AuditActions.ProviderEventRejected, linked.Id, "consent-revoked", ProviderEventOutcome.Unsigned),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC8, 09 section 10: on the Google route each failure is answered
    /// 400 in the shape RFC 8935 section 2.3 fixes, with the <c>err</c> of the first
    /// failure in the fixed order (a token that cannot be read or carries no
    /// <c>jti</c>, the key, the issuer, the audience, a passed <c>exp</c>), the same
    /// code as <c>description</c> and <c>Content-Language: en</c>, and changes nothing.
    /// </summary>
    /// <param name="failure">What is wrong with the token.</param>
    /// <param name="err">The code the failure is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("unreadable", "invalid_request")]
    [InlineData("eventless", "invalid_request")]
    [InlineData("unidentified", "invalid_request")]
    [InlineData("unidentified and forged", "invalid_request")]
    [InlineData("unpublished key", "invalid_key")]
    [InlineData("forged", "invalid_key")]
    [InlineData("unpublished key, another issuer", "invalid_key")]
    [InlineData("another issuer", "invalid_issuer")]
    [InlineData("another issuer, another audience", "invalid_issuer")]
    [InlineData("another audience", "invalid_audience")]
    [InlineData("another audience, expired", "invalid_audience")]
    [InlineData("expired", "invalid_request")]
    public async Task IDN_LIFE_012a_AC8_EachFailureOnTheGoogleRouteIsAnsweredWithItsErrAsync(string failure, string err)
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;
        JsonElement compromised = GoogleEvent(Risc + "sessions-revoked", GoogleSubject);
        long lapsed = _deployment.Clock.GetUtcNow().AddHours(-1).ToUnixTimeSeconds();
        SocialProvidersInMemory google = _deployment.SocialProviders;

        string token = failure switch
        {
            "unreadable" => "not.a-token",
            "eventless" => google.Signed(Factor.Google, "evt-1", "no event"),
            "unidentified" => google.Departing(Factor.Google, null, compromised),
            "unidentified and forged" => google.Forged(Factor.Google, string.Empty, compromised),
            "unpublished key" => google.Departing(Factor.Google, "evt-1", compromised, keyId: "google-key-0"),
            "forged" => google.Forged(Factor.Google, "evt-1", compromised),
            "unpublished key, another issuer" => google.Departing(
                Factor.Google,
                "evt-1",
                compromised,
                issuer: "https://accounts.elsewhere.test/",
                keyId: "google-key-0"),
            "another issuer" => google.Departing(
                Factor.Google,
                "evt-1",
                compromised,
                issuer: "https://accounts.elsewhere.test/"),
            "another issuer, another audience" => google.Departing(
                Factor.Google,
                "evt-1",
                compromised,
                issuer: "https://accounts.elsewhere.test/",
                audience: "another-client.apps.google.test"),
            "another audience" => google.Departing(
                Factor.Google,
                "evt-1",
                compromised,
                audience: "another-client.apps.google.test"),
            "another audience, expired" => google.Departing(
                Factor.Google,
                "evt-1",
                compromised,
                audience: "another-client.apps.google.test",
                expires: lapsed),
            _ => google.Departing(Factor.Google, "evt-1", compromised, expires: lapsed),
        };

        Answer answered = await DeliveredAsync(Google, token);

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Equal("application/json", answered.Header(HeaderNames.ContentType));
        Assert.Equal("en", answered.Header(HeaderNames.ContentLanguage));
        Assert.Equal(err, answered.Text("err"));
        Assert.Equal(err, answered.Text("description"));
        Assert.Equal(2, answered.Json().EnumerateObject().Count());
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.DoesNotContain(
            _deployment.CredentialAudit.ProviderEvents,
            recorded => recorded.Action == AuditActions.ProviderEventTaken);
    }

    /// <summary>
    /// IDN-LIFE-012a AC8, 09 section 10: an event stating a lifetime that has not
    /// passed is carried, as one stating none is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC8_AnEventWhoseLifetimeHasNotPassedIsCarriedAsync()
    {
        (SubjectId subject, _) = await LinkedAsync(Factor.Google, GoogleSubject);

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Departing(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject),
                expires: _deployment.Clock.GetUtcNow().AddHours(1).ToUnixTimeSeconds()));

        Assert.Equal(StatusCodes.Status202Accepted, answered.Status);
        Assert.Empty(Live(subject));
    }

    /// <summary>
    /// IDN-LIFE-012a AC9, 09 section 10: on the Google route a token whose <c>nbf</c> is
    /// later than now is answered 400 <c>invalid_request</c>, by a second as by an hour
    /// since the instant gets no leeway, after the audience and the <c>exp</c> are
    /// judged, and changes nothing.
    /// </summary>
    /// <param name="ahead">How far after now the token says it holds from, in seconds.</param>
    /// <param name="departure">What else is wrong with the token.</param>
    /// <param name="err">The code the failure is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(1, "nothing", "invalid_request")]
    [InlineData(3600, "nothing", "invalid_request")]
    [InlineData(3600, "expired", "invalid_request")]
    [InlineData(3600, "another audience", "invalid_audience")]
    public async Task IDN_LIFE_012a_AC9_AnEventNotYetValidOnTheGoogleRouteIsAnsweredInvalidRequestAsync(
        int ahead,
        string departure,
        string err)
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Departing(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject),
                audience: departure is "another audience" ? "another-client.apps.google.test" : null,
                expires: departure is "expired" ? now.AddHours(-1).ToUnixTimeSeconds() : null,
                notBefore: now.AddSeconds(ahead).ToUnixTimeSeconds()));

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Equal("application/json", answered.Header(HeaderNames.ContentType));
        Assert.Equal("en", answered.Header(HeaderNames.ContentLanguage));
        Assert.Equal(err, answered.Text("err"));
        Assert.Equal(err, answered.Text("description"));
        Assert.Equal(2, answered.Json().EnumerateObject().Count());
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.DoesNotContain(
            _deployment.CredentialAudit.ProviderEvents,
            recorded => recorded.Action == AuditActions.ProviderEventTaken);
    }

    /// <summary>
    /// IDN-LIFE-012a AC9, 09 section 10: an event whose <c>nbf</c> is now or earlier is
    /// carried, and so is one carrying no <c>nbf</c>, which is not refused for it.
    /// </summary>
    /// <param name="behind">
    /// How far before now the token says it holds from, in seconds, or nothing for a
    /// token carrying no <c>nbf</c>.
    /// </param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3600)]
    public async Task IDN_LIFE_012a_AC9_AnEventValidNowOrStatingNoNbfIsCarriedAsync(int? behind)
    {
        (SubjectId subject, _) = await LinkedAsync(Factor.Google, GoogleSubject);
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Departing(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject),
                notBefore: behind is int seconds ? now.AddSeconds(-seconds).ToUnixTimeSeconds() : null));

        Assert.Equal(StatusCodes.Status202Accepted, answered.Status);
        Assert.Empty(Live(subject));
    }

    /// <summary>
    /// IDN-LIFE-012a AC8, 09 section 10: an event on the Google route of a deployment
    /// that declared no such provider is answered <c>invalid_issuer</c>, and one that
    /// cannot be read is answered <c>invalid_request</c> before the provider is looked
    /// for.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC8_AnEventOfAProviderNotDeclaredIsAnsweredInvalidIssuerAsync()
    {
        await using var undeclared = new Deployment(providers: []);

        Prepared(undeclared);

        Answer unknown = await new Machine(undeclared).DeliverAsync(
            Google,
            Delivered,
            undeclared.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));
        Answer unreadable = await new Machine(undeclared).DeliverAsync(Google, Delivered, "not.a-token");

        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Status);
        Assert.Equal("en", unknown.Header(HeaderNames.ContentLanguage));
        Assert.Equal("invalid_issuer", unknown.Text("err"));
        Assert.Equal("invalid_issuer", unknown.Text("description"));
        Assert.Equal(StatusCodes.Status400BadRequest, unreadable.Status);
        Assert.Equal("invalid_request", unreadable.Text("err"));
    }

    /// <summary>
    /// IDN-LIFE-012a AC7: an event carrying no <c>jti</c> changes nothing and is
    /// refused as unreadable on both routes, before anything is claimed or looked up:
    /// <c>invalid_request</c> on the Google route, a rejected callback on the Apple
    /// route.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC7_AnEventCarryingNoJtiChangesNothingAndIsRefusedAsync()
    {
        (SubjectId subject, Authenticator google) = await LinkedAsync(Factor.Google, GoogleSubject);
        Authenticator apple = await LinkAsync(subject, Factor.Apple, AppleSubject);
        int live = Live(subject).Count;

        Answer pushed = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Departing(
                Factor.Google,
                null,
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));
        Answer posted = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Departing(
                Factor.Apple,
                null,
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status400BadRequest, pushed.Status);
        Assert.Equal("invalid_request", pushed.Text("err"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, posted.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), posted.Text("code"));
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(google).State);
        Assert.Equal(AuthenticatorState.Active, Held(apple).State);
        Assert.Empty(_deployment.CredentialAudit.ProviderEvents);
    }

    /// <summary>
    /// IDN-LIFE-012a AC8, 09 section 10: on either route a provider document the
    /// library cannot read refuses nothing. The delivery is answered 500
    /// <c>system.fault</c>, its unit of work is rolled back so nothing is claimed,
    /// recorded or changed, and the same event delivered again once the document reads
    /// is carried.
    /// </summary>
    /// <param name="path">The route the event arrives on.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(Google)]
    [InlineData(Apple)]
    public async Task IDN_LIFE_012a_AC8_AProviderDocumentThatCannotBeReadIsAFaultAndClaimsNothingAsync(string path)
    {
        bool google = path == Google;
        (SubjectId subject, Authenticator linked) = google
            ? await LinkedAsync(Factor.Google, GoogleSubject)
            : await LinkedAsync(Factor.Apple, AppleSubject);
        int live = Live(subject).Count;
        string body = google
            ? _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject))
            : Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject)));
        int committed = _deployment.Work.Committed;
        int rolledBack = _deployment.Work.RolledBack;

        _deployment.SocialProviders.Reachable = false;

        Answer faulted = await DeliveredAsync(path, body);

        Assert.Equal(StatusCodes.Status500InternalServerError, faulted.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), faulted.Text("code"));
        Assert.Empty(faulted.Json().GetProperty("details").EnumerateObject());
        Assert.Null(faulted.Header(HeaderNames.RetryAfter));
        Assert.Equal(committed, _deployment.Work.Committed);
        Assert.Equal(rolledBack + 1, _deployment.Work.RolledBack);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Empty(_deployment.CredentialAudit.ProviderEvents);
        Assert.Contains(
            _deployment.Logs.Lines,
            line => line.Contains("providers/" + (google ? "google" : "apple") + " could not be read", StringComparison.Ordinal));

        _deployment.SocialProviders.Reachable = true;

        Answer carried = await DeliveredAsync(path, body);

        Assert.Equal(google ? StatusCodes.Status202Accepted : StatusCodes.Status200OK, carried.Status);
        Assert.Equal(AuditActions.ProviderEventTaken, Assert.Single(_deployment.CredentialAudit.ProviderEvents).Action);
    }

    /// <summary>
    /// IDN-LIFE-012a AC1: an event delivered again once the person has signed back in
    /// changes nothing, is audited as rejected, and is still acknowledged, so the
    /// provider stops delivering it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC1_AReplayedEventChangesNothingAndIsAuditedAsRejectedAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        string token = _deployment.SocialProviders.Signed(
            Factor.Google,
            "evt-1",
            GoogleEvent(Risc + "sessions-revoked", GoogleSubject));

        _ = await DeliveredAsync(Google, token);
        await SignedInByPasswordAsync();

        int live = Live(subject).Count;
        Answer replayed = await DeliveredAsync(Google, token);

        Assert.Equal(StatusCodes.Status202Accepted, replayed.Status);
        Assert.Equal(1, live);
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Equal(
            (AuditActions.ProviderEventRejected, linked.Id, Risc + "sessions-revoked", ProviderEventOutcome.Replayed),
            _deployment.CredentialAudit.ProviderEvents[^1]);
    }

    /// <summary>
    /// IDN-LIFE-012a: a credential a provider's event held stands again once the person
    /// signs in by another factor, and the restoration is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AHeldCredentialStandsAgainOnceThePersonSignsInByAnotherFactorAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);

        _ = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "account-disabled", GoogleSubject)));

        Assert.True(Held(linked).IsHeldByProvider);

        await SignedInByPasswordAsync();

        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Contains(
            (AuditActions.CredentialRestored, subject, linked.Id),
            _deployment.CredentialAudit.Records);
    }

    /// <summary>
    /// IDN-LIFE-012a AC2 and IDN-LIFE-012 AC2: consent withdrawn or the account deleted
    /// at Apple unlinks the credential where the account keeps another way in, and the
    /// security-notice set is told.
    /// </summary>
    /// <param name="type">The event's type.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("consent-revoked")]
    [InlineData("account-delete")]
    public async Task IDN_LIFE_012a_AC2_AWithdrawnIdentityIsUnlinkedAsync(string type)
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Apple, AppleSubject);
        int before = _deployment.Mail.Taken.Count;

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(Factor.Apple, "evt-1", AppleEvent(type, AppleSubject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.DoesNotContain(_deployment.Authenticators.All, held => held.Id == linked.Id);
        Assert.Equal(AccountState.Active, await StateAsync(subject));
        Assert.Contains(
            _deployment.Mail.Taken.Skip(before),
            mail => string.Equals(mail.Destination.Value, Flow.Address, StringComparison.Ordinal));
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, type, ProviderEventOutcome.CredentialUnlinked),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC2 and IDN-LIFE-012 AC3: where the withdrawn identity is the
    /// account's last way in, the credential stays and the account is suspended by the
    /// administrator's hand, its sessions end, and the security-notice set is told.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC2_AWithdrawnLastCredentialSuspendsTheAccountWithANoticeAsync()
    {
        var subject = SubjectId.New(_randomness);
        const string address = "only-apple@example.test";

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _deployment.Identifiers.Reads(subject, Language);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, address);

        Authenticator linked = await LinkAsync(subject, Factor.Apple, AppleSubject);

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Contains(_deployment.Authenticators.All, held => held.Id == linked.Id);
        Assert.Equal(AccountState.Suspended, await StateAsync(subject));
        Assert.Equal(
            SuspensionOrigin.Administrator,
            await _deployment.Accounts.SuspendedByAsync(subject, CancellationToken.None));
        Assert.Equal(
            SuspensionOrigin.Administrator,
            Assert.Single(_deployment.Events.Of<AccountSuspended>()).By);
        Assert.Contains(
            _deployment.Mail.Taken,
            mail => string.Equals(mail.Destination.Value, address, StringComparison.Ordinal));
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, "consent-revoked", ProviderEventOutcome.AccountSuspended),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18, IDN-LIFE-012a AC2: the notice of a suspension a provider
    /// event brings, refused by a restriction, fails nothing. The event is answered as
    /// it would have been, and the suspension, its announcement and the event's record
    /// are committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_AnEventWhoseNoticeIsRefusedIsTakenAndCommittedAsync()
    {
        var subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _deployment.Identifiers.Reads(subject, Language);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, "only-apple@example.test");

        Authenticator linked = await LinkAsync(subject, Factor.Apple, AppleSubject);

        _deployment.Configuration.Set(
            Settings.Restrictions,
            [
                .. Settings.Restrictions.Default,
                new Restriction(
                    "every.notice",
                    RestrictionKeyKind.Global,
                    null,
                    RestrictionPurpose.Notification,
                    [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
            ]);

        _deployment.SendLedger.Given(
            new RestrictionKey("every.notice", RestrictionKeyKind.Global, "every.notice"),
            _deployment.Clock.GetUtcNow());

        int rolledBack = _deployment.Work.RolledBack;

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(rolledBack, _deployment.Work.RolledBack);
        Assert.Empty(_deployment.Mail.Taken);
        Assert.Equal(AccountState.Suspended, await StateAsync(subject));
        _ = Assert.Single(_deployment.Events.Of<AccountSuspended>());
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, "consent-revoked", ProviderEventOutcome.AccountSuspended),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: an event whose change of state cannot be announced is
    /// answered with that failure, and its unit of work is rolled back before it is
    /// answered, so nothing of the event is committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnEventThatCannotBeAnnouncedRollsBackAsync()
    {
        var subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _deployment.Identifiers.Reads(subject, Language);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, "only-apple@example.test");

        _ = await LinkAsync(subject, Factor.Apple, AppleSubject);

        _deployment.Events.Refusal = Error.From(ErrorCodes.SystemFault);

        int committed = _deployment.Work.Committed;
        int rolledBack = _deployment.Work.RolledBack;

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status500InternalServerError, answered.Status);
        Assert.Empty(_deployment.CredentialAudit.ProviderEvents);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(committed, _deployment.Work.Committed);
        Assert.Equal(rolledBack + 1, _deployment.Work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, IDN-LIFE-012a: a rejected provider event commits its
    /// rejection's record with its counts, in one unit of work, and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARejectedProviderEventCommitsItsRecordAndItsCountsAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;
        int committed = _deployment.Work.OutermostCommitted;
        int rolledBack = _deployment.Work.RolledBack;

        Answer answered = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Forged(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(committed + 1, _deployment.Work.OutermostCommitted);
        Assert.Equal(rolledBack, _deployment.Work.RolledBack);
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        _ = Assert.Single(_deployment.CredentialAudit.ProviderEvents);
    }

    /// <summary>
    /// IDN-LIFE-012a AC2, IDN-LIFE-013, CONV-DESIGN-003 AC6: a deletion begun while the
    /// withdrawal waited for the account's row is found under the lock, so the account
    /// is left to its deletion and no suspension is made or announced.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC2_ADeletionBegunMeanwhileIsLeftToItAsync()
    {
        var subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _deployment.Identifiers.Reads(subject, Language);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, "only-apple-deleting@example.test");

        Authenticator linked = await LinkAsync(subject, Factor.Apple, AppleSubject);

        _deployment.Accounts.Holding = held => _deployment.Accounts.Stands(held, AccountState.Deleting);

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal(AccountState.Deleting, await StateAsync(subject));
        Assert.Empty(_deployment.Events.Of<AccountSuspended>());
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, "consent-revoked", ProviderEventOutcome.Recorded),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC2 (D-166, 286): where the account's other way in is a credential a
    /// provider's event holds, nothing usable may begin a sign-in without the withdrawn
    /// identity, so the credential stays and the account is suspended instead.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC2_AWithdrawnIdentityWhoseOtherWayInIsHeldSuspendsTheAccountAsync()
    {
        var subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _deployment.Identifiers.Reads(subject, Language);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, "apple-and-google@example.test");

        Authenticator google = await LinkAsync(subject, Factor.Google, GoogleSubject);
        Authenticator apple = await LinkAsync(subject, Factor.Apple, AppleSubject);

        _ = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "account-disabled", GoogleSubject)));

        Assert.True(Held(google).IsHeldByProvider);

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-2",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Contains(_deployment.Authenticators.All, held => held.Id == apple.Id);
        Assert.Equal(AccountState.Suspended, await StateAsync(subject));
        Assert.Equal(
            (AuditActions.ProviderEventTaken, apple.Id, "consent-revoked", ProviderEventOutcome.AccountSuspended),
            _deployment.CredentialAudit.ProviderEvents[^1]);
    }

    /// <summary>
    /// IDN-LIFE-012a: the address Apple stopped forwarding to drops to unverified.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AnAddressTheProviderStoppedForwardingToDropsToUnverifiedAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Apple, AppleSubject);

        Answer answered = await DeliveredAsync(
            Apple,
            Wrapped(_deployment.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("email-disabled", AppleSubject, Flow.Address))));

        HeldIdentifiers held = await _deployment.Identifiers.HeldAsync(subject, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.False(held.OfKind(IdentifierKind.Email).Single(email => email.Canonical == Flow.Address).IsVerified);
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, "email-disabled", ProviderEventOutcome.AddressUnverified),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a: an event the library does not act on is recorded and changes
    /// nothing, and one about an identity no account links is acknowledged and
    /// recorded nowhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AnEventThatChangesNothingIsRecordedAndAcknowledgedAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;

        Answer enabled = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "account-enabled", GoogleSubject)));
        Answer stranger = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-2",
                GoogleEvent(Risc + "sessions-revoked", "999999999999999999999")));

        Assert.Equal(StatusCodes.Status202Accepted, enabled.Status);
        Assert.Equal(StatusCodes.Status202Accepted, stranger.Status);
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Equal(
            (AuditActions.ProviderEventTaken, linked.Id, Risc + "account-enabled", ProviderEventOutcome.Recorded),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
    }

    /// <summary>
    /// IDN-LIFE-012a AC3 and BFF-MACH-001 AC2: the endpoint is on the machine profile,
    /// so a delivery carrying a browser's session cookie is refused and changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC3_TheEndpointIsOnTheMachineProfileAsync()
    {
        (SubjectId subject, Authenticator linked) = await LinkedAsync(Factor.Google, GoogleSubject);
        int live = Live(subject).Count;

        Answer refused = await new Machine(_deployment).DeliverAsync(
            Google,
            Delivered,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)),
            BrowserCookies.Session + "=stale");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
    }

    /// <summary>
    /// IDN-LIFE-012a AC3 and INT-GEN-003: the endpoint is counted against
    /// <c>integration.callback.ratelimit</c> like every callback, and a source over it
    /// is refused before anything it sent is read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC3_TheEndpointIsRateLimitedLikeEveryCallbackAsync()
    {
        (SubjectId subject, _) = await LinkedAsync(Factor.Google, GoogleSubject);

        _deployment.Configuration.Set(Settings.IntegrationCallbackRateLimit, 1);

        Answer first = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "account-enabled", GoogleSubject)));
        int live = Live(subject).Count;
        Answer limited = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-2",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));

        Assert.Equal(StatusCodes.Status202Accepted, first.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), limited.Text("code"));
        Assert.Equal(live, Live(subject).Count);
    }

    /// <summary>
    /// IDN-LIFE-012a AC3, 09 section 10: on the Apple route an event of a provider the
    /// deployment declared nothing for verifies against nothing and is refused as a
    /// callback refused for anything but its rate is: 422 with no interval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AC3_AnEventOfAProviderNotDeclaredIsRefusedOnTheAppleRouteAsync()
    {
        await using var undeclared = new Deployment(providers: []);

        Prepared(undeclared);

        Answer unknown = await new Machine(undeclared).DeliverAsync(
            Apple,
            "application/json",
            Wrapped(undeclared.SocialProviders.Signed(
                Factor.Apple,
                "evt-1",
                AppleEvent("consent-revoked", AppleSubject))));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unknown.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), unknown.Text("code"));
        Assert.Null(unknown.Header(HeaderNames.RetryAfter));
    }

    // A deployment that can send the notice a removed credential or a suspended account
    // carries.
    private static void Prepared(Deployment deployment)
    {
        Flow.Prepare(deployment);

        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            deployment.Templates.Set(
                MessageKind.SecurityNotice,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "notice" : null, "body"));
        }
    }

    // RISC: one event keyed by its type, naming the identity by issuer and subject.
    private static JsonElement GoogleEvent(string type, string subject) =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [type] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["subject"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["subject_type"] = "iss-sub",
                    ["iss"] = "https://accounts.google.test/",
                    ["sub"] = subject,
                },
            },
        });

    // Sign in with Apple writes its one event as the text of an object.
    private static string AppleEvent(string type, string subject, string? email = null)
    {
        var written = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["sub"] = subject,
            ["event_time"] = 1772366400000,
        };

        if (email is not null)
        {
            written["email"] = email;
            written["is_private_email"] = "true";
        }

        return JsonSerializer.Serialize(written);
    }

    private static string Wrapped(string token) =>
        JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["payload"] = token });

    private Task<Answer> DeliveredAsync(string path, string body) =>
        new Machine(_deployment).DeliverAsync(
            path,
            path == Google ? Delivered : "application/json",
            body);

    // An account registered as a person registers, holding a password, an email and a
    // phone and signed in once, with the provider's identity linked to it.
    private async Task<(SubjectId Subject, Authenticator Linked)> LinkedAsync(Factor provider, string subject)
    {
        _ = await Flow.SignedInAsync(_deployment);

        SubjectId account = _deployment.Directory.Created[^1].Subject;

        return (account, await LinkAsync(account, provider, subject));
    }

    private async Task<Authenticator> LinkAsync(SubjectId account, Factor provider, string subject)
    {
        Assert.True(CredentialLabel.TryParse("Linked identity", out CredentialLabel label));

        var linked = Authenticator.Linked(
            AuthenticatorId.New(_deployment.Clock),
            account,
            provider,
            label,
            _deployment.Clock.GetUtcNow());

        await _deployment.Authenticators.LinkAsync(linked, subject, CancellationToken.None);

        return linked;
    }

    // The password sign-in a browser makes, with the new-device check out of the way,
    // since what signing in by another factor does is what the test is about.
    private async Task SignedInByPasswordAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer completed = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal("complete", completed.Text("status"));
    }

    private IReadOnlyList<Session> Live(SubjectId subject) =>
        [.. _deployment.Sessions.All.Where(session => session.Subject == subject && session.EndedAt is null)];

    private Authenticator Held(Authenticator linked) =>
        _deployment.Authenticators.All.Single(held => held.Id == linked.Id);

    private async Task<AccountState?> StateAsync(SubjectId subject) =>
        await _deployment.Accounts.StateAsync(subject, CancellationToken.None);
}
