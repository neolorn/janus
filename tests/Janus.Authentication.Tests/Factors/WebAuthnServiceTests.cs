using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// What a WebAuthn ceremony has to reach for a credential to exist, and what a
/// credential has to reach to answer with one (AUTH-FACT-010 to AUTH-FACT-014).
/// </summary>
[Trait("kind", "unit")]
public sealed class WebAuthnServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly CredentialAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// The deployment as the tests configure it, unless a test says otherwise.
    /// </summary>
    public WebAuthnServiceTests() =>
        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.example.com", "https://id.example.com"]);

    private WebAuthnService Service =>
        new(_authenticators, _audit, _configuration, _work, _clock, _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-002b AC1: a passkey ceremony asks for a discoverable credential and
    /// a security-key ceremony does not, both under the identifier in force.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_TheCeremonyRunsUnderTheIdentifierInForceAsync()
    {
        WebAuthnCeremony passkey = Value(await Service.BeginAsync(
            Factor.Passkey,
            TestContext.Current.CancellationToken));
        WebAuthnCeremony key = Value(await Service.BeginAsync(
            Factor.SecurityKey,
            TestContext.Current.CancellationToken));

        Assert.Equal("example.com", passkey.RelyingPartyId);
        Assert.Equal([-8, -7, -257], passkey.Algorithms);
        Assert.True(passkey.DiscoverableCredential);
        Assert.False(key.DiscoverableCredential);
        Assert.NotEqual(passkey.Challenge, key.Challenge);
    }

    /// <summary>
    /// AUTH-FACT-010 AC3: a deployment whose relying party identifier sits over none
    /// of its origins enrols nothing; the refusal comes before the credential.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_010_AC3_ARefusedConfigurationEnrolsNothingAsync()
    {
        _configuration.Set(Settings.WebAuthnRelyingPartyId, "example.net");

        await Assert.ThrowsAsync<StartupException>(async () => await Service.CompleteAsync(
            Subject(),
            Factor.Passkey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        Assert.Empty(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014 AC2: a credential from an authenticator that attested to nothing
    /// enrols, attestation being asked of none of them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC2_AnUnattestedCredentialEnrolsAsync()
    {
        SubjectId subject = Subject();

        AuthenticatorId id = Value(await Service.CompleteAsync(
            subject,
            Factor.Passkey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        Authenticator enrolled = Assert.Single(_authenticators.All);

        Assert.Equal(id, enrolled.Id);
        Assert.Equal(Factor.Passkey, enrolled.Factor);
        Assert.True(enrolled.IsUsable);
        Assert.Equal("example.com", enrolled.WebAuthn!.RelyingPartyId);
    }

    /// <summary>
    /// AUTH-FACT-014 AC4: a credential using an algorithm outside the allow-list is
    /// refused at enrolment.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC4_AnAlgorithmOutsideTheAllowListIsRefusedAsync()
    {
        Assert.Equal(
            ErrorCodes.WebAuthnAlgorithmNotAllowed,
            Refusal(await Service.CompleteAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                Registration() with { Algorithm = -37 },
                TestContext.Current.CancellationToken)));

        Assert.Empty(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014 AC4: what the allow-list admits is what the deployment
    /// configured, not what the library prefers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC4_TheAllowListIsWhatTheDeploymentConfiguredAsync()
    {
        _configuration.Set(Settings.WebAuthnAlgorithms, (IReadOnlyList<int>)[-7]);

        Assert.Equal(
            ErrorCodes.WebAuthnAlgorithmNotAllowed,
            Refusal(await Service.CompleteAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                Registration() with { Algorithm = -257 },
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC1: a ceremony that verified nobody is refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC1_AnEnrolmentWithoutUserVerificationIsRefusedAsync() =>
        Assert.Equal(
            ErrorCodes.WebAuthnUserVerificationRequired,
            Refusal(await Service.CompleteAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                Registration() with { UserVerified = false },
                TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-014 AC1: an authentication without user verification is rejected.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC1_AnAssertionWithoutUserVerificationIsRejectedAsync()
    {
        await EnrolledAsync(Subject(), Registration());

        Assert.Equal(
            ErrorCodes.WebAuthnUserVerificationRequired,
            Refusal(await Service.PresentAsync(
                Assertion() with { UserVerified = false },
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC3: a counter lower than the stored value is rejected and the
    /// event audited.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_ACounterMovingBackwardsIsRejectedAndAuditedAsync()
    {
        SubjectId subject = Subject();
        AuthenticatorId id = await EnrolledAsync(subject, Registration() with { Counter = 9 });

        Assert.Equal(
            ErrorCodes.WebAuthnCounterMismatch,
            Refusal(await Service.PresentAsync(
                Assertion() with { Counter = 8 },
                TestContext.Current.CancellationToken)));

        (AuditAction action, SubjectId audited, AuthenticatorId credential) =
            Assert.Single(_audit.Records);

        Assert.Equal("auth.credential.countermismatch", action.ToString());
        Assert.Equal(subject, audited);
        Assert.Equal(id, credential);
    }

    /// <summary>
    /// AUTH-FACT-014 AC3: a counter standing still is a credential that exists twice,
    /// which is refused as one that moved backwards is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_ACounterStandingStillIsRejectedAsync()
    {
        await EnrolledAsync(Subject(), Registration() with { Counter = 9 });

        Assert.Equal(
            ErrorCodes.WebAuthnCounterMismatch,
            Refusal(await Service.PresentAsync(
                Assertion() with { Counter = 9 },
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014: an authenticator that keeps no counter reports nought at every
    /// assertion, which the check passes over rather than reading as a clone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AnAuthenticatorKeepingNoCounterIsAcceptedTwiceAsync()
    {
        await EnrolledAsync(Subject(), Registration() with { Counter = 0 });

        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 0 },
            TestContext.Current.CancellationToken)));
        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 0 },
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC3: a counter that advanced is accepted and recorded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_ACounterThatAdvancedIsRecordedAsync()
    {
        AuthenticatorId id = await EnrolledAsync(Subject(), Registration() with { Counter = 9 });

        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 10 },
            TestContext.Current.CancellationToken)));

        Authenticator held =
            (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!;

        Assert.Equal(10u, held.WebAuthn!.Counter);
        Assert.Equal(Noon, held.LastUsedAt);
        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-FACT-013 AC1, AC2: both flags are persisted, so a synced credential and a
    /// device-bound one are distinguishable after registration.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_013_AC1_BackupEligibilityAndStateAreRecordedAsync()
    {
        SubjectId subject = Subject();

        AuthenticatorId synced = await EnrolledAsync(
            subject,
            Registration() with { BackupEligible = true, BackupState = true });
        AuthenticatorId bound = await EnrolledAsync(
            subject,
            Registration() with
            {
                CredentialId = new byte[] { 9, 9, 9 },
                BackupEligible = false,
                BackupState = false,
            });

        WebAuthnMaterial first =
            (await _authenticators.FindAsync(synced, TestContext.Current.CancellationToken))!.WebAuthn!;
        WebAuthnMaterial second =
            (await _authenticators.FindAsync(bound, TestContext.Current.CancellationToken))!.WebAuthn!;

        Assert.True(first.BackupEligible);
        Assert.True(first.BackupState);
        Assert.False(second.BackupEligible);
        Assert.False(second.BackupState);
    }

    /// <summary>
    /// AUTH-FACT-011: the identifier in force is recorded against the credential.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_011_TheIdentifierIsRecordedAgainstTheCredentialAsync()
    {
        AuthenticatorId id = await EnrolledAsync(Subject(), Registration());

        Assert.Equal(
            "example.com",
            (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!
                .WebAuthn!.RelyingPartyId);
    }

    /// <summary>
    /// AUTH-FACT-011 AC1, AC2: a credential enrolled under a previous identifier is
    /// detected from the record alone, no authentication being needed to find it, and
    /// is refused where it is presented.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_011_AC1_ACredentialUnderAPreviousIdentifierIsDetectedAsync()
    {
        SubjectId subject = Subject();
        await EnrolledAsync(subject, Registration());

        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.example.net"]);

        Authenticator stale = Assert.Single(
            await Service.StaleAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal("example.com", stale.WebAuthn!.RelyingPartyId);
        Assert.Equal(
            ErrorCodes.WebAuthnRelyingPartyChanged,
            Refusal(await Service.PresentAsync(
                Assertion() with { RelyingPartyId = "example.net" },
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-002b AC3: an upgrade completes a passkey registration and retires
    /// the second-factor entry it came from.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_011_AnUpgradedSecurityKeyRetiresItsEntryAsync()
    {
        SubjectId subject = Subject();
        AuthenticatorId key = Value(await Service.CompleteAsync(
            subject,
            Factor.SecurityKey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        AuthenticatorId passkey = Value(await Service.UpgradeAsync(
            subject,
            key,
            Label(),
            Registration() with { CredentialId = new byte[] { 4, 5, 6 } },
            TestContext.Current.CancellationToken));

        Authenticator retired =
            (await _authenticators.FindAsync(key, TestContext.Current.CancellationToken))!;
        Authenticator standing =
            (await _authenticators.FindAsync(passkey, TestContext.Current.CancellationToken))!;

        Assert.Equal(AuthenticatorState.Invalidated, retired.State);
        Assert.False(retired.IsUsable);
        Assert.Equal(Factor.Passkey, standing.Factor);
        Assert.True(standing.IsUsable);
    }

    /// <summary>
    /// AUTH-FACT-002b AC3: an upgrade whose ceremony is refused changes nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_011_ARefusedUpgradeChangesNothingAsync()
    {
        SubjectId subject = Subject();
        AuthenticatorId key = Value(await Service.CompleteAsync(
            subject,
            Factor.SecurityKey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.WebAuthnAlgorithmNotAllowed,
            Refusal(await Service.UpgradeAsync(
                subject,
                key,
                Label(),
                Registration() with { Algorithm = -37 },
                TestContext.Current.CancellationToken)));

        Authenticator standing =
            (await _authenticators.FindAsync(key, TestContext.Current.CancellationToken))!;

        Assert.Equal(AuthenticatorState.Active, standing.State);
        Assert.Single(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014: a credential a ceremony never created is not one to upgrade.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task UpgradeAsync_ACredentialOfAnotherAccount_IsRefusedAsync()
    {
        AuthenticatorId key = Value(await Service.CompleteAsync(
            Subject(),
            Factor.SecurityKey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.UpgradeAsync(
                Subject(),
                key,
                Label(),
                Registration(),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014: a kind no ceremony creates begins none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BeginAsync_AKindNoCeremonyCreates_IsRefusedAsync() =>
        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.BeginAsync(Factor.Totp, TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-014: an assertion naming a credential the deployment does not hold
    /// is refused without saying which of the two it is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PresentAsync_ACredentialTheStoreDoesNotHold_IsRefusedAsync() =>
        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion(),
                TestContext.Current.CancellationToken)));

    private static WebAuthnRegistration Registration() =>
        new(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            Algorithm: -7,
            RelyingPartyId: "example.com",
            UserVerified: true,
            BackupEligible: false,
            BackupState: false,
            Counter: 0);

    private static WebAuthnAssertion Assertion() =>
        new(
            new byte[] { 1, 2, 3 },
            RelyingPartyId: "example.com",
            UserVerified: true,
            Counter: 0);

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("this phone", out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw Failed(error));

    private static ErrorCode? Refusal<TValue>(Result<TValue> result) =>
        result.Match<ErrorCode?>(_ => null, error => error.Code);

    private static Xunit.Sdk.XunitException Failed(Error error) =>
        new(error.Code.ToString());

    private SubjectId Subject() => SubjectId.New(_randomness);

    private async ValueTask<AuthenticatorId> EnrolledAsync(
        SubjectId subject,
        WebAuthnRegistration registration) =>
        Value(await Service.CompleteAsync(
            subject,
            Factor.Passkey,
            Label(),
            registration,
            TestContext.Current.CancellationToken));
}
