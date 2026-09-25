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
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
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
    /// nothing, is refused as every rejected callback is, and is audited as rejected
    /// against the account whose linked identity it names.
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

        Assert.Equal(StatusCodes.Status429TooManyRequests, answered.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), answered.Text("code"));
        Assert.Equal(live, Live(subject).Count);
        Assert.Equal(AuthenticatorState.Active, Held(linked).State);
        Assert.Equal(
            (AuditActions.ProviderEventRejected, linked.Id, Risc + "sessions-revoked", ProviderEventOutcome.Unsigned),
            Assert.Single(_deployment.CredentialAudit.ProviderEvents));
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
    /// IDN-LIFE-012a: an event of a provider the deployment declared nothing for, and
    /// one whose provider's keys cannot be read, verify against nothing and are refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_012a_AnEventNothingDeclaredOrReadableVerifiesIsRefusedAsync()
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

        _deployment.SocialProviders.Reachable = false;

        Answer unreadable = await DeliveredAsync(
            Google,
            _deployment.SocialProviders.Signed(
                Factor.Google,
                "evt-1",
                GoogleEvent(Risc + "sessions-revoked", GoogleSubject)));

        Assert.Equal(StatusCodes.Status429TooManyRequests, unknown.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, unreadable.Status);
        Assert.Contains(
            _deployment.Logs.Lines,
            line => line.Contains("providers/google could not be read", StringComparison.Ordinal));
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
