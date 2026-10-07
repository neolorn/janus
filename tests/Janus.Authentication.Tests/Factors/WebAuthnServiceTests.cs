using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Tests.Passwords;
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
    // A challenge as the library draws one: base64url, which is what the verification
    // decodes it from.
    private const string Challenge = "YS1jaGFsbGVuZ2U";

    private const string Origin = "https://app.example.com";

    private const string RelyingPartyId = "example.com";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
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
        new(_authenticators, _passwords, _audit, _configuration, _work, _clock, _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-002b AC1: a passkey ceremony asks for a credential the authenticator
    /// keeps and a security-key ceremony asks for one it does not, which is what the
    /// two dialogs turn into their resident-key values (`18` FE-SEC-001).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC1_OnlyThePasskeyCeremonyIsDiscoverableAsync()
    {
        WebAuthnCeremony passkey = Value(await Service.BeginAsync(
            Factor.Passkey,
            Person(Without()),
            TestContext.Current.CancellationToken));
        WebAuthnCeremony key = Value(await Service.BeginAsync(
            Factor.SecurityKey,
            Person(Without()),
            TestContext.Current.CancellationToken));

        Assert.True(passkey.DiscoverableCredential);
        Assert.False(key.DiscoverableCredential);
    }

    /// <summary>
    /// AUTH-FACT-002b AC1: neither ceremony admits a credential the person did not
    /// prove themselves to, so neither dialog can ask for less than verification.
    /// </summary>
    /// <param name="kind">The kind being created.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(Factor.Passkey)]
    [InlineData(Factor.SecurityKey)]
    public async Task AUTH_FACT_002b_AC1_NeitherKindIsCreatedWithoutUserVerificationAsync(
        Factor kind) =>
        Assert.Equal(
            ErrorCodes.WebAuthnUserVerificationRequired,
            Refusal(await Service.CompleteAsync(
                Subject(),
                kind,
                Label(),
                Registration() with { UserVerified = false },
                TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-014: both ceremonies run under the identifier in force and over the
    /// algorithms it admits, and no two carry the same challenge.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_TheCeremonyRunsUnderTheIdentifierInForceAsync()
    {
        WebAuthnCeremony passkey = Value(await Service.BeginAsync(
            Factor.Passkey,
            Person(Without()),
            TestContext.Current.CancellationToken));
        WebAuthnCeremony key = Value(await Service.BeginAsync(
            Factor.SecurityKey,
            Person(Without()),
            TestContext.Current.CancellationToken));

        Assert.Equal("example.com", passkey.RelyingPartyId);
        Assert.Equal("example.com", key.RelyingPartyId);
        Assert.Equal([-8, -7, -257], passkey.Algorithms);
        Assert.NotEqual(passkey.Challenge, key.Challenge);
    }

    /// <summary>
    /// REG-PM-001 AC1: the handle is the subject identifier and nothing else, so the
    /// one field an authenticator stores and offers unprompted carries an opaque
    /// value the library drew from randomness and no fact about the person.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_AC1_TheHandleIsTheSubjectIdentifierAndNothingElseAsync()
    {
        SubjectId subject = Without();

        WebAuthnCeremony ceremony = Value(await Service.BeginAsync(
            Factor.Passkey,
            Person(subject),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            subject.Value,
            new Guid(Base64Url.DecodeFromChars(ceremony.User.Id), bigEndian: true));
        Assert.Equal(16, Base64Url.DecodeFromChars(ceremony.User.Id).Length);
    }

    /// <summary>
    /// REG-PM-001: the ceremony carries the primary email as the name and the display
    /// name as the display name, which is what an authenticator shows when it offers
    /// the credential back.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_TheCeremonyCarriesTheNameAndTheDisplayNameAsync()
    {
        WebAuthnCeremony ceremony = Value(await Service.BeginAsync(
            Factor.Passkey,
            Person(Without()),
            TestContext.Current.CancellationToken));

        Assert.Equal("person@example.com", ceremony.User.Name);
        Assert.Equal("A Person", ceremony.User.DisplayName);
        Assert.Equal("example.com", ceremony.RelyingPartyId);
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
    /// AUTH-FACT-010 AC6: a deployment whose origin is configured with its host in
    /// Unicode admits a ceremony whose client data carries that origin with its host in
    /// ASCII form, which is how a browser writes it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_010_AC6_AnOriginConfiguredInUnicodeAdmitsACeremonyInAsciiFormAsync()
    {
        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.bücher.de"]);
        _configuration.Set(Settings.WebAuthnRelyingPartyId, "bücher.de");

        WebAuthnMaterial read = Value(await Service.ReadAsync(
            Factor.Passkey,
            Attestation(Challenge, "https://app.xn--bcher-kva.de", "xn--bcher-kva.de"),
            Challenge,
            TestContext.Current.CancellationToken));

        Assert.Equal("xn--bcher-kva.de", read.RelyingPartyId);
    }

    /// <summary>
    /// AUTH-FACT-010: a ceremony's origin is matched ordinally against the origins as
    /// held, so one that carries another port, or the host as no browser writes it, is
    /// admitted by none.
    /// </summary>
    /// <param name="origin">The origin the client data carries.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("https://app.xn--bcher-kva.de:8443")]
    [InlineData("https://app.bücher.de")]
    [InlineData("http://app.xn--bcher-kva.de")]
    public async Task AUTH_FACT_010_ACeremonyFromAnOriginNotHeldIsRefusedAsync(string origin)
    {
        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.bücher.de"]);
        _configuration.Set(Settings.WebAuthnRelyingPartyId, "bücher.de");

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.ReadAsync(
                Factor.Passkey,
                Attestation(Challenge, origin, "xn--bcher-kva.de"),
                Challenge,
                TestContext.Current.CancellationToken)));
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
                identified: true,
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

        _work.Reset();

        Assert.Equal(
            ErrorCodes.WebAuthnCounterMismatch,
            Refusal(await Service.PresentAsync(
                Assertion() with { Counter = 8 },
                identified: true,
                TestContext.Current.CancellationToken)));

        (AuditAction action, SubjectId audited, AuthenticatorId credential) =
            Assert.Single(_audit.Records);

        Assert.Equal("auth.credential.countermismatch", action.ToString());
        Assert.Equal(subject, audited);
        Assert.Equal(id, credential);

        // The refusal's record is committed, and the stored counter is left as it was.
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(
            9u,
            (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.WebAuthn!.Counter);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a credential invalidated while its assertion waited for
    /// the lock is refused, and the refusal ends the unit of work it was decided in
    /// with nothing committed.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACredentialInvalidatedMeanwhileRollsBackAsync()
    {
        await EnrolledAsync(Subject(), Registration());

        _authenticators.Locking = credential => credential.Invalidate();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion(),
                identified: true,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_audit.Records);
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
                identified: true,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC3 (WebAuthn Level 3 section 7.2): the check applies where either
    /// counter is above nought, so a counter of nought presented against a stored one
    /// above it is not above the stored value: it is rejected and audited, and the
    /// stored counter is left as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_ACounterOfNoughtAgainstAStoredCounterIsRejectedAndAuditedAsync()
    {
        SubjectId subject = Subject();
        AuthenticatorId id = await EnrolledAsync(subject, Registration() with { Counter = 9 });

        _work.Reset();

        ErrorCode? refused = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 0 },
            identified: true,
            TestContext.Current.CancellationToken));

        Authenticator held = (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!;
        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, refused);
        Assert.Equal(
            ("auth.credential.countermismatch", subject, id),
            Assert.Single(_audit.Records.Select(record => (record.Item1.ToString(), record.Item2, record.Item3))));
        Assert.Equal((9u, null), (held.WebAuthn!.Counter, held.LastUsedAt));
        Assert.Equal((1, 0), (_work.Committed, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-014 AC3: where nothing is stored, a first counter above nought is above
    /// the stored value, so it is accepted and recorded, and the same counter presented
    /// again has not advanced.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_AFirstCounterAboveNoughtIsRecordedAndTheSameOneAgainIsRejectedAsync()
    {
        AuthenticatorId id = await EnrolledAsync(Subject(), Registration() with { Counter = 0 });

        ErrorCode? first = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 5 },
            identified: true,
            TestContext.Current.CancellationToken));
        ErrorCode? again = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 5 },
            identified: true,
            TestContext.Current.CancellationToken));

        Assert.Null(first);
        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, again);
        Assert.Equal(
            5u,
            (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.WebAuthn!.Counter);
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
            identified: true,
            TestContext.Current.CancellationToken)));
        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 0 },
            identified: true,
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
            identified: true,
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
                identified: true,
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
    /// AUTH-FACT-014: a credential a ceremony never created is not one to upgrade,
    /// and one of another account answers as one that does not exist.
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
            ErrorCodes.CredentialNotFound,
            Refusal(await Service.UpgradeAsync(
                Subject(),
                key,
                Label(),
                Registration(),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PM-001: the handle an authenticator returns is what says whose credential
    /// answered, so one naming another account is refused as a wrong credential is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_AnAssertionWhoseHandleNamesAnotherAccountIsRefusedAsync()
    {
        SubjectId subject = Subject();

        await EnrolledAsync(subject, Registration());

        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { UserHandle = WebAuthnService.Handle(subject) },
            identified: true,
            TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion() with { UserHandle = WebAuthnService.Handle(Without()) },
                identified: true,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PM-001 AC4: where the ceremony was opened for no named account, an assertion
    /// that returns no user handle names nobody and is refused, where the same
    /// assertion returning the handle is accepted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_AnAssertionWithNoHandleIsRefusedWhereNoAccountWasNamedAsync()
    {
        SubjectId subject = Subject();

        await EnrolledAsync(subject, Registration());

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion(),
                identified: false,
                TestContext.Current.CancellationToken)));
        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion() with { UserHandle = WebAuthnService.Handle(subject), Counter = 0 },
            identified: false,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PM-001 AC4: a second-step security key answers a ceremony opened for the
    /// account the sign-in named and returns no handle, and is judged as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_ASecondStepKeyWithNoHandleIsJudgedAsBeforeAsync()
    {
        _ = Value(await Service.CompleteAsync(
            Subject(),
            Factor.SecurityKey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken));

        Assert.Null(Refusal(await Service.PresentAsync(
            Assertion(),
            identified: true,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PM-001: a handle that is not one the library ever issued names no account,
    /// which is refused for the same reason a handle naming another one is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_AnAssertionWhoseHandleIsNotOneWeIssuedIsRefusedAsync()
    {
        await EnrolledAsync(Subject(), Registration());

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion() with { UserHandle = "not-a-handle" },
                identified: true,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PM-001 and PRIV-RIGHT-005a AC18: a handle carrying the max UUID, which no
    /// subject can be made from, names no account and is refused as a stranger's is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_AnAssertionWhoseHandleIsTheMaxUuidIsRefusedAsync()
    {
        await EnrolledAsync(Subject(), Registration());

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.PresentAsync(
                Assertion() with { UserHandle = Base64Url.EncodeToString(Guid.AllBitsSet.ToByteArray(bigEndian: true)) },
                identified: true,
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
            Refusal(await Service.BeginAsync(Factor.Totp, Person(Without()), TestContext.Current.CancellationToken)));

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
                identified: true,
                TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-002b AC2: a security key as a second step is refused on an account
    /// that holds no password, and a passkey on the same account is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC2_ASecondStepIsRefusedWithoutAPasswordAsync()
    {
        SubjectId subject = Without();

        Assert.Equal(
            ErrorCodes.FactorPasswordRequired,
            Refusal(await Service.CompleteAsync(
                subject,
                Factor.SecurityKey,
                Label(),
                Registration(),
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));

        Assert.Null(Refusal(await Service.CompleteAsync(
            subject,
            Factor.Passkey,
            Label(),
            Registration(),
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-011 AC2: what a change of identifier made stale is answered from the
    /// records in one query, so nothing compares the identifier in force against a
    /// credential at a sign-in: the query runs with no ceremony and no authentication
    /// behind it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_011_AC2_WhatIsStaleIsAnsweredFromTheRecordsAloneAsync()
    {
        SubjectId subject = Subject();
        await EnrolledAsync(subject, Registration());

        Assert.Empty(await Service.StaleAsync(subject, TestContext.Current.CancellationToken));

        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.example.net"]);

        Assert.Single(await Service.StaleAsync(subject, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-FACT-013 AC2: after registration the two are told apart by the flag the
    /// redundancy rule reads, and by nothing about the authenticator's make.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_013_AC2_ASyncedCredentialIsToldApartFromADeviceBoundOneAsync()
    {
        SubjectId subject = Subject();

        AuthenticatorId synced = await EnrolledAsync(
            subject,
            Registration() with { BackupEligible = true, BackupState = true });
        AuthenticatorId bound = await EnrolledAsync(
            subject,
            Registration() with { CredentialId = new byte[] { 9, 9, 9 } });

        Assert.True(await SyncedAsync(synced));
        Assert.False(await SyncedAsync(bound));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a suspended key is judged as an active one would be. An
    /// assertion that passes every check is refused <c>auth.credential.suspended</c>
    /// and writes neither its counter nor its use, and one that fails a check is
    /// refused as that check refuses an active key's.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedKeyIsRefusedSuspendedOnlyWhereItsAssertionIsAcceptedAsync()
    {
        AuthenticatorId id = await EnrolledAsync(Subject(), Registration() with { Counter = 9 });
        Authenticator key = (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!;
        key.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        _work.Reset();

        ErrorCode? accepted = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 10 },
            identified: true,
            TestContext.Current.CancellationToken));
        ErrorCode? unverified = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 10, UserVerified = false },
            identified: true,
            TestContext.Current.CancellationToken));
        ErrorCode? moved = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = 8 },
            identified: true,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CredentialSuspended, accepted);
        Assert.Equal(ErrorCodes.WebAuthnUserVerificationRequired, unverified);
        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, moved);
        Assert.Equal(9u, key.WebAuthn!.Counter);
        Assert.Null(key.LastUsedAt);
        Assert.False(_work.Open);
        Assert.Single(_audit.Records);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC9, AUTH-FACT-014 AC3: a suspended key goes through the counter
    /// check an active one does, first. One whose counter did not advance, equal to the
    /// stored one, below it, or nought against a stored one, is refused
    /// <c>auth.webauthn.countermismatch</c> and audited as an active key's is, and is
    /// told nothing of the suspension.
    /// </summary>
    /// <param name="counter">The counter the assertion carries, against a stored 9.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(9u)]
    [InlineData(8u)]
    [InlineData(0u)]
    public async Task AUTH_RECOV_007_AC9_ASuspendedKeyWhoseCounterDidNotAdvanceIsRefusedACounterMismatchAsync(
        uint counter)
    {
        SubjectId subject = Subject();
        AuthenticatorId id = await EnrolledAsync(subject, Registration() with { Counter = 9 });
        Authenticator key = (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!;
        key.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        _work.Reset();

        ErrorCode? refused = Refusal(await Service.PresentAsync(
            Assertion() with { Counter = counter },
            identified: true,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, refused);
        Assert.Equal(
            ("auth.credential.countermismatch", subject, id),
            Assert.Single(_audit.Records.Select(record => (record.Item1.ToString(), record.Item2, record.Item3))));
        Assert.Equal((9u, null), (key.WebAuthn!.Counter, key.LastUsedAt));
        Assert.Equal((1, 0), (_work.Committed, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a key suspended while its assertion waited for the lock is
    /// refused <c>auth.credential.suspended</c>, and the refusal ends the unit of work
    /// it was decided in with nothing committed.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_AKeySuspendedMeanwhileIsRefusedSuspendedAsync()
    {
        await EnrolledAsync(Subject(), Registration());
        _authenticators.Locking = credential => credential.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        _work.Reset();

        ErrorCode? refused = Refusal(await Service.PresentAsync(
            Assertion(),
            identified: true,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CredentialSuspended, refused);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-FACT-014 AC6: a ceremony whose authenticator chooses EdDSA under the
    /// default allow-list enrols and its assertions verify, as one choosing either
    /// other algorithm of the list does. The key is kept as the attested credential
    /// data gives it.
    /// </summary>
    /// <param name="algorithm">The COSE algorithm the authenticator chooses.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(SoftwareAuthenticator.EdDsa)]
    [InlineData(SoftwareAuthenticator.Es256)]
    [InlineData(SoftwareAuthenticator.Rs256)]
    public async Task AUTH_FACT_014_AC6_ACeremonyOfAnAlgorithmOfTheDefaultAllowListEnrolsAndItsAssertionsVerifyAsync(
        int algorithm)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding(algorithm);

        AuthenticatorId id = await CreatedAsync(subject, authenticator);
        WebAuthnMaterial enrolled = await HeldAsync(id);

        Assert.Equal(algorithm, enrolled.Algorithm);
        Assert.Equal(authenticator.CredentialId, enrolled.CredentialId.ToArray());
        Assert.Equal(authenticator.PublicKey, enrolled.PublicKey.ToArray());
        Assert.Null(await AssertedAsync(subject, authenticator.Asserted(Challenge, Origin, RelyingPartyId)));
        Assert.Equal(Noon, (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
    }

    /// <summary>
    /// AUTH-FACT-014 AC6: an assertion is verified against the key the credential was
    /// enrolled with, so one another key of the same algorithm signed is rejected, and
    /// so is one signed over another challenge.
    /// </summary>
    /// <param name="algorithm">The COSE algorithm of the credential.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(SoftwareAuthenticator.EdDsa)]
    [InlineData(SoftwareAuthenticator.Es256)]
    [InlineData(SoftwareAuthenticator.Rs256)]
    public async Task AUTH_FACT_014_AC6_AnAssertionTheCredentialsKeyDidNotSignIsRejectedAsync(int algorithm)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding(algorithm);
        var forger = SoftwareAuthenticator.Holding(algorithm, authenticator.CredentialId);

        AuthenticatorId id = await CreatedAsync(subject, authenticator);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            await AssertedAsync(subject, forger.Asserted(Challenge, Origin, RelyingPartyId)));
        Assert.Equal(
            ErrorCodes.FactorRejected,
            await AssertedAsync(subject, authenticator.Asserted("YW5vdGhlcg", Origin, RelyingPartyId)));
        Assert.Null((await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
    }

    /// <summary>
    /// AUTH-FACT-014 AC4: a ceremony whose authenticator chose an algorithm the
    /// deployment's allow-list does not hold is refused at enrolment under the code it
    /// always had.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC4_ACeremonyOfAnAlgorithmOutsideTheAllowListIsRefusedAsync()
    {
        _configuration.Set(Settings.WebAuthnAlgorithms, (IReadOnlyList<int>)[-7]);

        Assert.Equal(
            ErrorCodes.WebAuthnAlgorithmNotAllowed,
            Refusal(await Service.EnrolAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                SoftwareAuthenticator.Holding(SoftwareAuthenticator.EdDsa).Attested(Challenge, Origin, RelyingPartyId),
                Challenge,
                upgrading: null,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014 AC1: a ceremony whose authenticator data does not carry the user
    /// verified flag is refused at enrolment and at sign-in under the code it always
    /// had.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC1_ACeremonyWhoseAuthenticatorVerifiedNobodyIsRefusedAsync()
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();

        Assert.Equal(
            ErrorCodes.WebAuthnUserVerificationRequired,
            Refusal(await Service.EnrolAsync(
                subject,
                Factor.Passkey,
                Label(),
                authenticator.Attested(Challenge, Origin, RelyingPartyId, SoftwareAuthenticator.UserPresent),
                Challenge,
                upgrading: null,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_authenticators.All);

        _ = await CreatedAsync(subject, authenticator);

        Assert.Equal(
            ErrorCodes.WebAuthnUserVerificationRequired,
            await AssertedAsync(
                subject,
                authenticator.Asserted(Challenge, Origin, RelyingPartyId, SoftwareAuthenticator.UserPresent)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC7: a ceremony whose client data carries an origin of
    /// <c>webauthn.relatedorigins</c> is admitted, at enrolment and at sign-in, and one
    /// from an origin of neither setting is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC7_ACeremonyFromARelatedOriginIsAdmittedAsync()
    {
        const string related = "https://app.example.org";

        _configuration.Set(Settings.WebAuthnRelatedOrigins, (IReadOnlyList<string>)[related]);

        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();

        AuthenticatorId id = Value(await Service.EnrolAsync(
            subject,
            Factor.Passkey,
            Label(),
            authenticator.Attested(Challenge, related, RelyingPartyId),
            Challenge,
            upgrading: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(RelyingPartyId, (await HeldAsync(id)).RelyingPartyId);
        Assert.Null(await AssertedAsync(subject, authenticator.Asserted(Challenge, related, RelyingPartyId)));
        Assert.Equal(
            ErrorCodes.FactorRejected,
            await AssertedAsync(
                subject,
                authenticator.Asserted(Challenge, "https://app.example.net", RelyingPartyId, counter: 1)));
        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.ReadAsync(
                Factor.Passkey,
                SoftwareAuthenticator.Holding().Attested(Challenge, "https://app.example.net", RelyingPartyId),
                Challenge,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: client data whose <c>crossOrigin</c> is true is refused at
    /// enrolment, nothing enrolled, and client data that says false is admitted as
    /// client data that says nothing is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC8_ACreationWhoseClientDataIsCrossOriginIsRefusedAsync()
    {
        SubjectId subject = Subject();

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.EnrolAsync(
                subject,
                Factor.Passkey,
                Label(),
                SoftwareAuthenticator.Holding().Attested(Challenge, Origin, RelyingPartyId, crossOrigin: true),
                Challenge,
                upgrading: null,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_authenticators.All);

        _ = Value(await Service.EnrolAsync(
            subject,
            Factor.Passkey,
            Label(),
            SoftwareAuthenticator.Holding().Attested(Challenge, Origin, RelyingPartyId, crossOrigin: false),
            Challenge,
            upgrading: null,
            TestContext.Current.CancellationToken));

        _ = Assert.Single(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: an assertion whose client data's <c>crossOrigin</c> is true is
    /// refused and nothing of it is stored, and one that says false is accepted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC8_AnAssertionWhoseClientDataIsCrossOriginIsRefusedAsync()
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();
        AuthenticatorId id = await CreatedAsync(subject, authenticator);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            await AssertedAsync(
                subject,
                authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 3, crossOrigin: true)));
        Assert.Null((await HeldAsync(id)).Counter);
        Assert.Null((await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
        Assert.Null(await AssertedAsync(
            subject,
            authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 3, crossOrigin: false)));
        Assert.Equal(3u, (await HeldAsync(id)).Counter);
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: an assertion whose BE flag differs from the stored
    /// <c>backupEligible</c> is refused, whichever way it differs, and nothing of it is
    /// stored.
    /// </summary>
    /// <param name="enrolled">The flags the credential was created with.</param>
    /// <param name="presented">The flags the assertion carries.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(0x05, 0x0D)]
    [InlineData(0x05, 0x1D)]
    [InlineData(0x0D, 0x05)]
    [InlineData(0x1D, 0x05)]
    public async Task AUTH_FACT_014_AC8_AnAssertionWhoseBackupEligibilityDiffersFromTheStoredOneIsRefusedAsync(
        byte enrolled,
        byte presented)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();
        AuthenticatorId id = await CreatedAsync(subject, authenticator, enrolled);
        WebAuthnMaterial before = await HeldAsync(id);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            await AssertedAsync(
                subject,
                authenticator.Asserted(Challenge, Origin, RelyingPartyId, presented, counter: 4)));

        WebAuthnMaterial after = await HeldAsync(id);

        Assert.Equal(
            (before.BackupEligible, before.BackupState, before.Counter),
            (after.BackupEligible, after.BackupState, after.Counter));
        Assert.Null((await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-FACT-014 AC8: an assertion whose BS flag changed updates the stored
    /// <c>backupState</c>, in either direction, the credential's eligibility standing as
    /// it was.
    /// </summary>
    /// <param name="enrolled">The flags the credential was created with.</param>
    /// <param name="presented">The flags the assertion carries.</param>
    /// <param name="before">The backup state the record holds from the creation.</param>
    /// <param name="after">The backup state the record holds afterwards.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(0x0D, 0x1D, false, true)]
    [InlineData(0x1D, 0x0D, true, false)]
    [InlineData(0x1D, 0x1D, true, true)]
    public async Task AUTH_FACT_014_AC8_AnAssertionWhoseBackupStateChangedUpdatesTheStoredOneAsync(
        byte enrolled,
        byte presented,
        bool before,
        bool after)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();
        AuthenticatorId id = await CreatedAsync(subject, authenticator, enrolled);

        Assert.Equal((true, before), await FlagsAsync());
        Assert.Null(await AssertedAsync(
            subject,
            authenticator.Asserted(Challenge, Origin, RelyingPartyId, presented)));
        Assert.Equal((true, after), await FlagsAsync());

        async ValueTask<(bool Eligible, bool State)> FlagsAsync()
        {
            WebAuthnMaterial held = await HeldAsync(id);

            return (held.BackupEligible, held.BackupState);
        }
    }

    /// <summary>
    /// AUTH-FACT-014, AC3: each assertion stores the counter it carries, and one whose
    /// counter did not advance past the stored one is refused under the code it always
    /// had, the stored counter standing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AC3_EachAssertionStoresItsCounterAndOneThatDidNotAdvanceIsRefusedAsync()
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();
        AuthenticatorId id = await CreatedAsync(subject, authenticator);

        Assert.Null(await AssertedAsync(
            subject,
            authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 5)));
        Assert.Equal(5u, (await HeldAsync(id)).Counter);
        Assert.Null(await AssertedAsync(
            subject,
            authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 9)));
        Assert.Equal(9u, (await HeldAsync(id)).Counter);

        foreach (uint counter in new uint[] { 9, 4, 0 })
        {
            Assert.Equal(
                ErrorCodes.WebAuthnCounterMismatch,
                await AssertedAsync(
                    subject,
                    authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: counter)));
        }

        Assert.Equal(9u, (await HeldAsync(id)).Counter);
        Assert.Equal(3, _audit.Records.Count);
    }

    /// <summary>
    /// AUTH-FACT-014: an extension output the library did not ask for, in the
    /// authenticator data of either ceremony, is ignored: the credential enrols and its
    /// assertion verifies.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_AnExtensionOutputTheLibraryDidNotAskForIsIgnoredAsync()
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();

        AuthenticatorId id = Value(await Service.EnrolAsync(
            subject,
            Factor.Passkey,
            Label(),
            authenticator.Attested(Challenge, Origin, RelyingPartyId, extensions: true),
            Challenge,
            upgrading: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(authenticator.PublicKey, (await HeldAsync(id)).PublicKey.ToArray());
        Assert.Null(await AssertedAsync(
            subject,
            authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 2, extensions: true)));
        Assert.Equal(2u, (await HeldAsync(id)).Counter);
    }

    /// <summary>
    /// AUTH-FACT-014: the transports are stored as the response gives them, and none
    /// where it gives none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_TheTransportsAreStoredAsTheResponseGivesThemAsync()
    {
        SubjectId subject = Subject();

        AuthenticatorId first = Value(await Service.EnrolAsync(
            subject,
            Factor.Passkey,
            Label(),
            SoftwareAuthenticator.Holding().Attested(
                Challenge,
                Origin,
                RelyingPartyId,
                transports: ["internal", "hybrid", "a-transport-of-tomorrow"]),
            Challenge,
            upgrading: null,
            TestContext.Current.CancellationToken));
        WebAuthnMaterial read = Value(await Service.ReadAsync(
            Factor.Passkey,
            SoftwareAuthenticator.Holding().Attested(Challenge, Origin, RelyingPartyId),
            Challenge,
            TestContext.Current.CancellationToken));

        Assert.Equal(["internal", "hybrid", "a-transport-of-tomorrow"], (await HeldAsync(first)).Transports);
        Assert.Empty(read.Transports);
    }

    /// <summary>
    /// AUTH-FACT-014: a creation whose credential identifier is one a credential
    /// already holds is refused, the credential that holds it standing as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_014_ACreationWhoseCredentialIdentifierIsHeldIsRefusedAsync()
    {
        var authenticator = SoftwareAuthenticator.Holding();
        _ = await CreatedAsync(Subject(), authenticator);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.EnrolAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                SoftwareAuthenticator.Holding(credentialId: authenticator.CredentialId)
                    .Attested(Challenge, Origin, RelyingPartyId),
                Challenge,
                upgrading: null,
                TestContext.Current.CancellationToken)));
        _ = Assert.Single(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014: a creation ceremony the verification does not stand behind (one
    /// bound to another relying party, opened with another challenge, of another
    /// ceremony's type, of a credential type that is not a public key, or carrying
    /// bytes that are no client data or no attestation object) is refused as a factor
    /// is, and nothing is enrolled.
    /// </summary>
    /// <param name="fault">What is wrong with the ceremony.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("relyingParty")]
    [InlineData("challenge")]
    [InlineData("ceremony")]
    [InlineData("type")]
    [InlineData("identifier")]
    [InlineData("clientData")]
    [InlineData("attestationObject")]
    [InlineData("truncated")]
    [InlineData("encoding")]
    public async Task AUTH_FACT_014_ACreationTheVerificationDoesNotStandBehindIsRejectedAsync(string fault)
    {
        var authenticator = SoftwareAuthenticator.Holding();
        AuthenticatorAttestation sound = authenticator.Attested(Challenge, Origin, RelyingPartyId);
        AuthenticatorAssertion asserted = authenticator.Asserted(Challenge, Origin, RelyingPartyId);

        AuthenticatorAttestation answered = fault switch
        {
            "relyingParty" => authenticator.Attested(Challenge, Origin, "example.org"),
            "challenge" => authenticator.Attested("YW5vdGhlcg", Origin, RelyingPartyId),
            "ceremony" => sound with { ClientDataJson = asserted.ClientDataJson },
            "type" => sound with { Type = "password" },
            "identifier" => sound with { RawId = Base64Url.EncodeToString(new byte[] { 9, 9, 9 }) },
            "clientData" => sound with { ClientDataJson = Base64Url.EncodeToString(new byte[] { 1, 2, 3 }) },
            "attestationObject" => sound with { AttestationObject = Base64Url.EncodeToString(new byte[] { 1, 2, 3 }) },
            "truncated" => sound with { AttestationObject = sound.AttestationObject[..80] },
            _ => sound with { AttestationObject = "not base64url!" },
        };

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refusal(await Service.EnrolAsync(
                Subject(),
                Factor.Passkey,
                Label(),
                answered,
                Challenge,
                upgrading: null,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-014: an assertion the verification does not stand behind (one bound to
    /// another relying party's hash, of another ceremony's type, of a credential type
    /// that is not a public key, naming another credential than its identifier does, or
    /// carrying bytes that are no client data, no authenticator data or no signature)
    /// is refused as a factor is, and nothing of it is stored.
    /// </summary>
    /// <param name="fault">What is wrong with the assertion.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("relyingParty")]
    [InlineData("ceremony")]
    [InlineData("type")]
    [InlineData("identifier")]
    [InlineData("clientData")]
    [InlineData("authenticatorData")]
    [InlineData("signature")]
    [InlineData("userHandle")]
    [InlineData("encoding")]
    public async Task AUTH_FACT_014_AnAssertionTheVerificationDoesNotStandBehindIsRejectedAsync(string fault)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding(SoftwareAuthenticator.EdDsa);
        AuthenticatorId id = await CreatedAsync(subject, authenticator);
        AuthenticatorAssertion sound = authenticator.Asserted(Challenge, Origin, RelyingPartyId, counter: 6);
        AuthenticatorAttestation attested = authenticator.Attested(Challenge, Origin, RelyingPartyId);

        AuthenticatorAssertion answered = fault switch
        {
            "relyingParty" => authenticator.Asserted(Challenge, Origin, "example.org", counter: 6),
            "ceremony" => sound with { ClientDataJson = attested.ClientDataJson },
            "type" => sound with { Type = "password" },
            "identifier" => sound with { Id = Base64Url.EncodeToString(new byte[] { 9, 9, 9 }) },
            "clientData" => sound with { ClientDataJson = Base64Url.EncodeToString(new byte[] { 1, 2, 3 }) },
            "authenticatorData" => sound with { AuthenticatorData = Base64Url.EncodeToString(new byte[] { 1, 2, 3 }) },
            "signature" => sound with { Signature = Base64Url.EncodeToString(new byte[] { 1, 2, 3 }) },
            "userHandle" => sound with { UserHandle = "not base64url!" },
            _ => sound with { Signature = "not base64url!" },
        };

        Assert.Equal(ErrorCodes.FactorRejected, await AssertedAsync(subject, answered));
        Assert.Null((await HeldAsync(id)).Counter);
        Assert.Null((await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
    }

    /// <summary>
    /// AUTH-FACT-014: a credential whose stored key is not one of the algorithm it was
    /// enrolled with, or is no key the verification reads, is refused at sign-in and
    /// nothing of the assertion is stored.
    /// </summary>
    /// <param name="unreadable">Whether the stored key is bytes that are no key.</param>
    /// <param name="refusal">The refusal.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false, "auth.webauthn.algorithmnotallowed")]
    [InlineData(true, "auth.factor.rejected")]
    public async Task AUTH_FACT_014_ACredentialWhoseStoredKeyIsNotOfItsAlgorithmIsRefusedAsync(
        bool unreadable,
        string refusal)
    {
        SubjectId subject = Subject();
        var authenticator = SoftwareAuthenticator.Holding();

        AuthenticatorId id = await EnrolledAsync(
            subject,
            Registration() with
            {
                CredentialId = authenticator.CredentialId,
                PublicKey = unreadable ? new byte[] { 4, 5, 6 } : authenticator.PublicKey,
                Algorithm = unreadable ? SoftwareAuthenticator.Es256 : SoftwareAuthenticator.EdDsa,
            });

        Assert.Equal(
            refusal,
            (await AssertedAsync(subject, authenticator.Asserted(Challenge, Origin, RelyingPartyId)))?.ToString());
        Assert.Null((await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.LastUsedAt);
    }

    // A credential the authenticator created at the deployment's origin, enrolled as a
    // passkey of the account.
    private async ValueTask<AuthenticatorId> CreatedAsync(
        SubjectId subject,
        SoftwareAuthenticator authenticator,
        byte flags = SoftwareAuthenticator.UserPresent | SoftwareAuthenticator.UserVerified) =>
        Value(await Service.EnrolAsync(
            subject,
            Factor.Passkey,
            Label(),
            authenticator.Attested(Challenge, Origin, RelyingPartyId, flags),
            Challenge,
            upgrading: null,
            TestContext.Current.CancellationToken));

    private async ValueTask<WebAuthnMaterial> HeldAsync(AuthenticatorId id) =>
        (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!.WebAuthn!;

    private async ValueTask<ErrorCode?> AssertedAsync(SubjectId subject, AuthenticatorAssertion answered) =>
        Refusal(await Service.AssertAsync(
            answered,
            Challenge,
            subject,
            TestContext.Current.CancellationToken));

    private static WebAuthnRegistration Registration() =>
        new(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            Algorithm: -7,
            RelyingPartyId: "example.com",
            UserVerified: true,
            BackupEligible: false,
            BackupState: false,
            Counter: 0,
            Transports: []);

    // What a browser sends back from a creation ceremony run at an origin: the client
    // data carrying that origin, and authenticator data bound to the relying party.
    private static AuthenticatorAttestation Attestation(string challenge, string origin, string relyingParty) =>
        SoftwareAuthenticator.Holding(credentialId: [1, 2, 3]).Attested(challenge, origin, relyingParty);

    // REG-PM-001: the handle is the subject identifier; the two shown values are the
    // account's own primary email and display name.
    private static CeremonyUser Person(SubjectId subject) =>
        new(WebAuthnService.Handle(subject), "person@example.com", "A Person");

    private static WebAuthnAssertion Assertion() =>
        new(
            new byte[] { 1, 2, 3 },
            RelyingPartyId: "example.com",
            UserVerified: true,
            Counter: 0,
            BackupEligible: false,
            BackupState: false);

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

    // A second-factor key is enrolled only on an account that holds a password
    // (AUTH-FACT-002b), and a passkey on either.
    private SubjectId Subject()
    {
        SubjectId subject = Without();

        _passwords.Hold(subject, Noon);

        return subject;
    }

    private SubjectId Without() => SubjectId.New(_randomness);

    private async ValueTask<bool> SyncedAsync(AuthenticatorId id) =>
        (await _authenticators.FindAsync(id, TestContext.Current.CancellationToken))!
            .WebAuthn!.BackupState;

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
