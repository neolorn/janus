using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// What an account reads and edits about itself: the whole of what it may see, the
/// profile fields a policy switches, the username and the preference set
/// (REG-ACCT-001, REG-PROF-001, REG-PREF-001, REG-IDENT-009).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountServiceTests : IAsyncDisposable
{
    private const string Source = "198.51.100.7";
    private const string Primary = "primary@example.test";
    private const string Chosen = "kestrel";
    private const string Taken = "harrier";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(
        Source,
        new DeviceDescription("Firefox", "Fedora"),
        Location: null);

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration("theme", PreferenceKind.Text, "system"),
        new PreferenceDeclaration("reducedMotion", PreferenceKind.Flag, "false"),
        new PreferenceDeclaration("tier", PreferenceKind.Text, "standard", AdministratorOnly: true),
    ]);

    private readonly AccountDirectoryInMemory _directory = new(Declared);
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly RecoveryCodeStoreInMemory _recoveryCodes = new();
    private readonly AccountAuditInMemory _audit = new();
    private readonly LifecycleLinkStoreInMemory _links = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly EventsInMemory _events = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _person;

    /// <summary>
    /// An active account holding one verified email and a password, which is what
    /// the shortest registration leaves behind.
    /// </summary>
    public AccountServiceTests()
    {
        _person = SubjectId.New(_randomness);
        _directory.Stands(_person, AccountState.Active);
        _passwords.Hold(_person, Noon);
        _ = _identifiers.Verified(_person, IdentifierKind.Email, Primary);
    }

    private AccountService Service =>
        new(
            new AccountLifecycle(
                _directory,
                _identifiers,
                _links,
                _sessions,
                new SendingService(
                    _configuration,
                    _ledger,
                    _templates,
                    _mail,
                    _sms,
                    RestrictionKeySuppliers.None,
                    Considered.Nothing(_work, _clock),
                    new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
                    _work,
                    _events,
                    _clock,
                    _randomness),
                _audit,
                Gate,
                _events,
                _configuration,
                _work,
                _clock,
                _randomness),
            _directory,
            _identifiers,
            _authenticators,
            _recoveryCodes,
            _audit,
            Gate,
            ReservedUsernames.Default,
            Declared,
            _configuration,
            _work,
            _clock);

    private StepUpGuard Gate => new(
        _sessions,
        _authenticators,
        _passwords,
        new PolicyResolution(_memberships, _configuration, _raises),
        _clock);

    private AccessContext Acting => AccessContext.Of(_person);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// REG-ACCT-001 AC1: one read carries the identifiers, the credentials, the
    /// profile and the preferences, and carries no field a policy switched off.
    /// </summary>
    [Fact]
    public async Task REG_ACCT_001_AC1_TheReadCarriesEveryGroupThePersonMaySeeAsync()
    {
        _authenticators.Hold(Passkey());
        _directory.Holds(_person, Profile("Kestrel", "Kestrel Ismail", new DateOnly(1990, 1, 1)));

        AccountDetail detail = Read(await Service.ReadAsync(
            Acting,
            TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Active, detail.State);
        Assert.Equal(Primary, Assert.Single(detail.Identifiers.Emails).Value);
        Assert.Equal(Factor.Passkey, Assert.Single(detail.Credentials).Kind);
        Assert.Equal("Kestrel", detail.Profile.DisplayName);
        Assert.Equal("system", detail.Preferences.Declared["theme"]);

        // The two fields this deployment does not collect, and the username it does
        // not enable, are absent from the answer rather than empty in it.
        Assert.Null(detail.Profile.LegalName);
        Assert.Null(detail.Profile.DateOfBirth);
        Assert.Null(detail.Identifiers.Username);
    }

    /// <summary>
    /// REG-PROF-001 AC2: with both keys off the edit refuses the field and the read
    /// carries neither, whatever the account already holds.
    /// </summary>
    [Fact]
    public async Task REG_PROF_001_AC2_ASwitchedOffFieldIsNeitherAcceptedNorReturnedAsync()
    {
        _directory.Holds(_person, Profile("Kestrel", "Kestrel Ismail", new DateOnly(1990, 1, 1)));

        Assert.Equal(
            ErrorCodes.ProfileNotAccepted,
            Refused(await Service.EditProfileAsync(
                Acting,
                Stepped(),
                new ProfileEdit(LegalName: "Kestrel Ismail"),
                TestContext.Current.CancellationToken)));

        ProfileDetail shown = Read(await Service.ReadAsync(
                Acting,
                TestContext.Current.CancellationToken))
            .Profile;

        Assert.Null(shown.LegalName);
        Assert.Null(shown.DateOfBirth);
    }

    /// <summary>
    /// REG-PROF-001 AC3: the date of birth is corrected through support, so the
    /// person's own edit is refused whether the deployment retains it or not.
    /// </summary>
    [Fact]
    public async Task REG_PROF_001_AC3_ThePersonDoesNotEditTheDateOfBirthAsync()
    {
        _configuration.Set(Settings.ProfileDateOfBirth, AttributeRequirement.Optional);
        _directory.Holds(_person, Profile("Kestrel", null, new DateOnly(1990, 1, 1)));

        Assert.Equal(
            ErrorCodes.ProfileNotAccepted,
            Refused(await Service.EditProfileAsync(
                Acting,
                Stepped(),
                new ProfileEdit(DateOfBirth: "1991-02-03"),
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            new DateOnly(1990, 1, 1),
            Read(await Service.ReadAsync(Acting, TestContext.Current.CancellationToken))
                .Profile
                .DateOfBirth);
    }

    /// <summary>
    /// REG-IDENT-009 AC2: the second change inside the window is refused, and the
    /// refusal carries the instant the next one may be made.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_009_AC2_ASecondChangeInsideTheWindowIsRefusedAsync()
    {
        _configuration.Set(Settings.IdentifiersUsernameEnabled, value: true);

        await ChosenAsync(Chosen);

        _clock.Advance(TimeSpan.FromDays(1));

        Error refused = Failure(await Service.EditProfileAsync(
            Acting,
            Stepped(),
            new ProfileEdit(Username: "merlin"),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.UsernameCoolingOff, refused.Code);
        Assert.Equal(
            Noon + Settings.IdentifiersUsernameChangeCoolOff.Default,
            refused.Details["retryAt"].Deserialize<DateTimeOffset>());

        Assert.Equal(Chosen, await UsernameAsync());
    }

    /// <summary>
    /// REG-IDENT-009 AC3: a username an erasure freed is held, and answers as taken
    /// until the hold elapses.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_009_AC3_AnErasedUsernameIsHeldUntilTheRetentionElapsesAsync()
    {
        _configuration.Set(Settings.IdentifiersUsernameEnabled, value: true);
        _identifiers.Holds(Taken, Noon + Settings.RetentionConsent.Default);

        Assert.Equal(
            ErrorCodes.UsernameTaken,
            Refused(await Service.EditProfileAsync(
                Acting,
                Stepped(),
                new ProfileEdit(Username: Taken),
                TestContext.Current.CancellationToken)));

        _clock.Advance(Settings.RetentionConsent.Default);

        await ChosenAsync(Taken);

        Assert.Equal(Taken, await UsernameAsync());
    }

    private static HeldProfile Profile(string? displayName, string? legalName, DateOnly? dateOfBirth)
    {
        DisplayName? shown = null;
        LegalName? legal = null;

        if (displayName is not null && DisplayName.TryParse(displayName, out DisplayName read))
        {
            shown = read;
        }

        if (legalName is not null && LegalName.TryParse(legalName, out LegalName named))
        {
            legal = named;
        }

        return new HeldProfile(shown, legal, dateOfBirth, null);
    }

    private static AccountDetail Read(Result<AccountDetail> result) =>
        result.Match(
            detail => detail,
            error => throw new InvalidOperationException("The read was refused: " + error.Code));

    private static Error Failure(Result result)
    {
        Error? refused = null;

        result.Switch(
            () => throw new InvalidOperationException("The operation was accepted."),
            error => refused = error);

        return refused!;
    }

    private static ErrorCode Refused(Result result) => Failure(result).Code;

    private static void Accepted(Result result) =>
        result.Switch(
            () => { },
            error => throw new InvalidOperationException("It was refused: " + error.Code));

    private static CredentialLabel Labelled(string label) =>
        CredentialLabel.TryParse(label, out CredentialLabel named)
            ? named
            : throw new InvalidOperationException("The label does not parse.");

    private Authenticator Passkey() =>
        Authenticator.WebAuthnCredential(
            AuthenticatorId.New(_clock),
            _person,
            Factor.Passkey,
            Labelled("This laptop"),
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "example.test",
                Counter: 0,
                BackupEligible: true,
                BackupState: true),
            Noon);

    private async Task ChosenAsync(string username)
    {
        Accepted(await Service.EditProfileAsync(
            Acting,
            Stepped(),
            new ProfileEdit(Username: username),
            TestContext.Current.CancellationToken));
    }

    private async Task<string?> UsernameAsync() =>
        Read(await Service.ReadAsync(Acting, TestContext.Current.CancellationToken))
            .Identifiers
            .Username;

    private SessionId Stepped()
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _person,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            _clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

        _sessions.AddAsync(session, Drawn(), Drawn(), TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    private byte[] Drawn()
    {
        byte[] fingerprint = new byte[32];

        _randomness.GetBytes(fingerprint);

        return fingerprint;
    }

    /// <summary>
    /// IDN-ATTR-008 AC1: nothing is marked, so the second step enrolled last is the
    /// one offered first, and the one enrolled after it takes its place.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC1_TheLatestSecondStepIsPreferredWhileNothingIsMarkedAsync()
    {
        Authenticator first = SecondStepKey("The old one", Noon);

        _authenticators.Hold(first);

        Assert.Equal(first.Id, await PreferredAsync());

        Authenticator second = SecondStepKey("The new one", Noon.AddDays(1));

        _authenticators.Hold(second);

        Assert.Equal(second.Id, await PreferredAsync());
    }

    /// <summary>
    /// IDN-ATTR-008 AC2: the preference names a second step the account holds, so a
    /// credential of another account, and one that is no second step, are refused
    /// alike.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC2_AMethodTheAccountDoesNotHoldIsRefusedAsync()
    {
        Authenticator passkey = Passkey();

        _authenticators.Hold(passkey);

        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refused(await Service.PreferSecondStepAsync(
                Acting,
                AuthenticatorId.New(_clock),
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refused(await Service.PreferSecondStepAsync(
                Acting,
                passkey.Id,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-ATTR-008 AC3: the mark goes with the credential it was on, so the
    /// preference falls to the latest of what remains and to nothing where nothing
    /// remains.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC3_RemovingThePreferredOneMovesThePreferenceAsync()
    {
        Authenticator older = SecondStepKey("The old one", Noon);
        Authenticator marked = SecondStepKey("The marked one", Noon.AddDays(1));

        _authenticators.Hold(older);
        _authenticators.Hold(marked);

        Accepted(await Service.PreferSecondStepAsync(
            Acting,
            marked.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(marked.Id, await PreferredAsync());

        await _authenticators.RemoveAsync(marked.Id, TestContext.Current.CancellationToken);

        Assert.Equal(older.Id, await PreferredAsync());

        await _authenticators.RemoveAsync(older.Id, TestContext.Current.CancellationToken);

        Assert.Null(await PreferredAsync());
    }

    // Which credential the account read says is offered first.
    private async Task<AuthenticatorId?> PreferredAsync()
    {
        AccountDetail detail = Read(await Service.ReadAsync(
            Acting,
            TestContext.Current.CancellationToken));

        foreach (CredentialSummary credential in detail.Credentials)
        {
            if (credential.Preferred)
            {
                return credential.Id;
            }
        }

        return null;
    }

    private Authenticator SecondStepKey(string label, DateTimeOffset at) =>
        Authenticator.WebAuthnCredential(
            AuthenticatorId.New(_clock),
            _person,
            Factor.SecurityKey,
            Labelled(label),
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "example.test",
                Counter: 0,
                BackupEligible: false,
                BackupState: false),
            at);
}
