using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.BreakGlass;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Records;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// What a host reads of another person's account: the account, its finding by an
/// identifier and the contact details, each asked in an organization and reaching the
/// people tied to it (IDN-READ-003, IDN-READ-004, D-218).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountReadTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
        [
            new PreferenceDeclaration("digest", PreferenceKind.Boolean, "true"),
            new PreferenceDeclaration("badge", PreferenceKind.Boolean, "false", AdministratorOnly: true),
        ]);

    private readonly AccountDirectoryInMemory _directory = new(Declared);
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly OrganizationsInMemory _organizations;
    private readonly AccountTiesInMemory _ties = new();
    private readonly RecordDirectoryInMemory _records = new();
    private readonly ReadVolumeInMemory _volume = new();
    private readonly AccountAuditInMemory _audit = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly OrganizationId _administering;
    private readonly OrganizationId _shop;
    private readonly SubjectId _clerk;
    private readonly SubjectId _customer;

    /// <summary>
    /// A deployment with its administrative organization and one other, a clerk of the
    /// other holding the three reads there, and a customer the other reaches.
    /// </summary>
    public AccountReadTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _administering = OrganizationId.New(_clock);
        _shop = OrganizationId.New(_clock);
        _administrative.Organization = _administering;
        _organizations.Seed(_administering, administrative: true);
        _organizations.Seed(_shop);
        _clerk = Account();
        _customer = Account();
        _ties.Tie(_customer, _shop);
        _memberships.Place(_customer, _shop);

        foreach (Permission permission in new[] { Permissions.AccountRead, Permissions.ContactRead })
        {
            _gate.Grant(_clerk, _shop, permission);
        }
    }

    private AccountAdministration Administration =>
        new(
            new AdministrativeScope(_gate, _administrative),
            new StepUpGuard(
                _sessions,
                new AuthenticatorStoreInMemory(),
                new PasswordStoreInMemory(),
                new PolicyResolution(_memberships, _configuration, new PolicyRaiseStoreInMemory()),
                _identifiers,
                new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock),
                _clock),
            _directory,
            new EmergencyAccountInMemory(),
            _sessions,
            new LifecycleLinkStoreInMemory(),
            _configuration,
            new EventsInMemory(),
            _audit,
            new ProfilePhotos(
                _directory,
                new SettingsRestrictionInMemory(),
                new PolicyResolution(_memberships, _configuration, new PolicyRaiseStoreInMemory()),
                _configuration,
                new AccountAuditInMemory(),
                _work,
                codec: null,
                _clock),
            new AccountReach(_gate, _administrative, _organizations, _ties),
            _identifiers,
            _records,
            _memberships,
            Declared,
            _volume,
            _work,
            _clock);

    private AccessContext Clerk => AccessContext.Of(_clerk);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-READ-003 AC1 (D-218): the account a host reads carries no credential, no date
    /// of birth and no photo, however much of each the account holds; and it carries the
    /// legal name only where <c>profile.legalname</c> is on.
    /// </summary>
    /// <param name="legalName">The switch of the legal name.</param>
    /// <param name="shown">Whether the legal name is answered.</param>
    [Theory]
    [InlineData(AttributeRequirement.Off, false)]
    [InlineData(AttributeRequirement.Optional, true)]
    public async Task IDN_READ_003_AC1_AnAccountReadAnswersNoCredentialDateOfBirthOrPhotoAsync(
        AttributeRequirement legalName,
        bool shown)
    {
        _configuration.Set(Settings.ProfileLegalName, legalName);
        _directory.Holds(_customer, new HeldProfile(
            Named<DisplayName>("Ann", DisplayName.TryParse),
            Named<LegalName>("Ann Example", LegalName.TryParse),
            new DateOnly(1990, 1, 2),
            Noon));
        _ = _identifiers.Verified(_customer, IdentifierKind.Email, "ann@example.test");

        AccountView account = Read(await Administration.ReadAsync(
            Clerk, _customer, _shop, TestContext.Current.CancellationToken));

        Assert.Equal(_customer, account.Subject);
        Assert.Equal(AccountState.Active, account.State);
        Assert.Equal(Noon, account.CreatedAt);
        Assert.Equal("Ann", account.Profile.DisplayName);
        Assert.Equal(shown ? "Ann Example" : null, account.Profile.LegalName);
        Assert.Equal(
            new AccountViewIdentifier("ann@example.test", Verified: true, Primary: true),
            Assert.Single(account.Identifiers.Emails));
        Assert.Equal([_shop], account.Memberships);
        Assert.Equal("true", account.Preferences.Declared["digest"]);
        Assert.DoesNotContain(
            Members(typeof(AccountView)),
            name => name.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || name.Contains("birth", StringComparison.OrdinalIgnoreCase)
                || name.Contains("photo", StringComparison.OrdinalIgnoreCase)
                || name.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// IDN-READ-003 AC2 (D-218): an account is found by its email address written in
    /// another case and in fullwidth letters, as sign-in reads it; an identifier no
    /// account bears is answered <c>identity.account.notfound</c>.
    /// </summary>
    [Fact]
    public async Task IDN_READ_003_AC2_AnAccountIsFoundByItsIdentifierInAnotherFormAsync()
    {
        _ = _identifiers.Verified(_customer, IdentifierKind.Email, "ann@example.test");

        Assert.Equal(
            _customer,
            Read(await Administration.FindAsync(
                Clerk, "Ａnn@Example.TEST", _shop, TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await Administration.FindAsync(
                Clerk, "nobody@example.test", _shop, TestContext.Current.CancellationToken)).Code);
    }

    /// <summary>
    /// IDN-READ-003 AC3 (D-218): the contact read answers the primary verified email
    /// address, and nothing for the phone where the account holds no verified number.
    /// </summary>
    [Fact]
    public async Task IDN_READ_003_AC3_TheContactReadAnswersThePrimaryVerifiedEmailAndPhoneAsync()
    {
        _ = _identifiers.Verified(_customer, IdentifierKind.Email, "ann@example.test");
        _ = _identifiers.Verified(_customer, IdentifierKind.Email, "ann.second@example.test");

        ContactDetail contact = Read(await Administration.ContactAsync(
            Clerk, _customer, _shop, TestContext.Current.CancellationToken));

        Assert.Equal("ann@example.test", contact.Email);
        Assert.Null(contact.Phone);
        Assert.Equal(AccountState.Active, contact.State);
    }

    /// <summary>
    /// REC-ADM-001 AC3: the contact read of a person record's subject answers its name,
    /// its contacts as held, verified or not, its language and its state, with no time
    /// zone and <c>kind</c> <c>record</c>; an account's names <c>account</c>.
    /// </summary>
    [Fact]
    public async Task REC_ADM_001_AC3_TheContactReadOfARecordAnswersItsNameContactsLanguageAndStateAsync()
    {
        SubjectId subject = Record(_shop, PersonRecordState.Restricted);

        ContactDetail contact = Read(await Administration.ContactAsync(
            Clerk, subject, _shop, TestContext.Current.CancellationToken));

        Assert.Equal(
            new ContactDetail(
                SubjectKind.Record,
                "Rana Haddad",
                "rana@example.test",
                "+441632960021",
                "ar",
                TimeZone: null,
                AccountState.Restricted),
            contact);
        Assert.Equal(
            SubjectKind.Account,
            Read(await Administration.ContactAsync(Clerk, _customer, _shop, TestContext.Current.CancellationToken)).Kind);
        Assert.Equal(2, _volume.Returned.Count);
    }

    /// <summary>
    /// REC-ADM-001 AC1: a record beyond the reach, and a deleted one, are answered exactly
    /// as a subject nothing bears.
    /// </summary>
    [Fact]
    public async Task REC_ADM_001_AC1_ARecordBeyondTheReachIsAnsweredAsNoneAsync()
    {
        var elsewhere = OrganizationId.New(_clock);

        _organizations.Seed(elsewhere);

        SubjectId beyond = Record(elsewhere, PersonRecordState.Active);
        SubjectId deleted = Record(_shop, PersonRecordState.Deleted);

        Error none = Refused(await Administration.ContactAsync(
            Clerk, new SubjectId(Guid.NewGuid()), _shop, TestContext.Current.CancellationToken));

        foreach (SubjectId subject in new[] { beyond, deleted })
        {
            Error refused = Refused(await Administration.ContactAsync(
                Clerk, subject, _shop, TestContext.Current.CancellationToken));

            Assert.Equal((none.Code, none.Details), (refused.Code, refused.Details));
        }
    }

    /// <summary>
    /// IDN-READ-004 AC1 (D-218): an account tied to no organization the read is asked in,
    /// and a deleted one, are answered exactly as a subject no account bears.
    /// </summary>
    [Fact]
    public async Task IDN_READ_004_AC1_AnAccountBeyondTheReachIsAnsweredAsNoneAsync()
    {
        SubjectId stranger = Account();
        SubjectId deleted = Account();

        _ties.Tie(deleted, _shop);
        _directory.Stands(deleted, AccountState.Deleted);

        Error none = Refused(await Administration.ContactAsync(
            Clerk, new SubjectId(Guid.NewGuid()), _shop, TestContext.Current.CancellationToken));

        foreach (SubjectId subject in new[] { stranger, deleted })
        {
            Error refused = Refused(await Administration.ReadAsync(
                Clerk, subject, _shop, TestContext.Current.CancellationToken));

            Assert.Equal((none.Code, none.Details), (refused.Code, refused.Details));
        }

        Assert.Equal(ErrorCodes.AccountNotFound, none.Code);
    }

    /// <summary>
    /// IDN-READ-004 (D-218), chapter 09 section 8a: an organization the deployment never
    /// held, or erased, is answered <c>identity.organization.notfound</c>; the reads
    /// asked where the caller holds nothing are refused <c>authz.denied</c>, the
    /// administrative organization where none is named included.
    /// </summary>
    [Fact]
    public async Task IDN_READ_004_TheOrganizationAskedInIsFoundAndItsPermissionHeldAsync()
    {
        var erased = OrganizationId.New(_clock);

        _organizations.Seed(erased, erasedAt: Noon);

        Assert.Equal(
            ErrorCodes.OrganizationNotFound,
            Refused(await Administration.ReadAsync(
                Clerk, _customer, OrganizationId.New(_clock), TestContext.Current.CancellationToken)).Code);
        Assert.Equal(
            ErrorCodes.OrganizationNotFound,
            Refused(await Administration.ReadAsync(
                Clerk, _customer, erased, TestContext.Current.CancellationToken)).Code);
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Administration.ReadAsync(
                Clerk, _customer, organization: null, TestContext.Current.CancellationToken)).Code);
    }

    /// <summary>
    /// IDN-READ-004 AC2 (D-218): a holder of the read in the administrative organization
    /// reaches an account no organization of theirs is tied to.
    /// </summary>
    [Fact]
    public async Task IDN_READ_004_AC2_AnAdministrativeHolderReachesEveryAccountAsync()
    {
        SubjectId steward = Account();
        SubjectId stranger = Account();

        _gate.Grant(steward, _administering, Permissions.ContactRead);

        Assert.Equal(
            AccountState.Active,
            Read(await Administration.ContactAsync(
                AccessContext.Of(steward), stranger, organization: null, TestContext.Current.CancellationToken)).State);
    }

    /// <summary>
    /// IDN-READ-004 AC4 (D-218): each account and contact a person reads is reported to
    /// the read volume, one record each, and a finding, which answers a subject alone, is
    /// not.
    /// </summary>
    [Fact]
    public async Task IDN_READ_004_AC4_EachPersonRecordReadIsCountedAsync()
    {
        _ = _identifiers.Verified(_customer, IdentifierKind.Email, "ann@example.test");

        _ = Read(await Administration.ReadAsync(Clerk, _customer, _shop, TestContext.Current.CancellationToken));
        _ = Read(await Administration.ContactAsync(Clerk, _customer, _shop, TestContext.Current.CancellationToken));
        _ = Read(await Administration.FindAsync(
            Clerk, "ann@example.test", _shop, TestContext.Current.CancellationToken));

        Assert.Equal(
            [(_clerk, 1), (_clerk, 1)],
            _volume.Returned.Select(counted => (counted.Context.Acting, counted.Records)));
    }

    /// <summary>
    /// IDN-READ-003 AC5 (D-218): an administrator holding <c>account:manage</c> in the
    /// administrative organization sets another person's administrator-only preference,
    /// with no step-up, and the change is recorded as the administrator's with the reason
    /// trimmed.
    /// </summary>
    [Fact]
    public async Task IDN_READ_003_AC5_AnAdministratorSetsAnAdministratorOnlyPreferenceAsync()
    {
        SubjectId steward = Account();

        _gate.Grant(steward, _administering, Permissions.AccountManage);

        Result set = await Administration.SetPreferencesAsync(
            AccessContext.Of(steward),
            _customer,
            "ar",
            "Asia/Riyadh",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["badge"] = "true" },
            "  Ticket 4471  ",
            TestContext.Current.CancellationToken);

        Assert.Null(set.Match<ErrorCode?>(() => null, error => error.Code));
        HeldPreferences held = await _directory.PreferencesAsync(_customer, TestContext.Current.CancellationToken);
        Assert.Equal(("ar", "Asia/Riyadh", "true"), (held.Language, held.TimeZone, held.Values["badge"]));
        RecordedChange recorded = Assert.Single(_audit.Recorded);
        Assert.Equal(
            (AuditActions.PreferencesChanged, steward, _customer, "Ticket 4471"),
            (recorded.Action, recorded.Acting, recorded.Subject, recorded.Reason));
    }

    /// <summary>
    /// IDN-READ-003 (D-218): the setting is refused <c>authz.denied</c> without
    /// <c>account:manage</c> in the administrative organization, held elsewhere included,
    /// <c>api.request.malformed</c> for a blank or overlong reason,
    /// <c>identity.account.notfound</c> for a subject no account bears and for an account
    /// deleted, and by name for a value the key does not admit; a refused setting changes
    /// and records nothing.
    /// </summary>
    [Fact]
    public async Task IDN_READ_003_AnAdministratorsSettingIsRefusedAsThePersonsOwnIsAsync()
    {
        SubjectId steward = Account();
        SubjectId deleted = Account();
        var badge = new Dictionary<string, string>(StringComparer.Ordinal) { ["badge"] = "true" };

        _gate.Grant(steward, _administering, Permissions.AccountManage);
        _gate.Grant(_clerk, _shop, Permissions.AccountManage);
        _directory.Stands(deleted, AccountState.Deleted);

        Assert.Equal(ErrorCodes.Denied, await SetRefusedAsync(_clerk, _customer, badge, "Ticket"));
        Assert.Equal(ErrorCodes.RequestMalformed, await SetRefusedAsync(steward, _customer, badge, "   "));
        Assert.Equal(
            ErrorCodes.RequestMalformed, await SetRefusedAsync(steward, _customer, badge, new string('r', 1025)));
        Assert.Equal(
            ErrorCodes.AccountNotFound, await SetRefusedAsync(steward, SubjectId.New(_randomness), badge, "Ticket"));
        Assert.Equal(ErrorCodes.AccountNotFound, await SetRefusedAsync(steward, deleted, badge, "Ticket"));
        Assert.Equal(
            ErrorCodes.PreferenceWrongType,
            await SetRefusedAsync(
                steward,
                _customer,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["badge"] = "often" },
                "Ticket"));
        Assert.Empty((await _directory.PreferencesAsync(_customer, TestContext.Current.CancellationToken)).Values);
        Assert.Empty(_audit.Recorded);
    }

    private async Task<ErrorCode?> SetRefusedAsync(
        SubjectId administrator,
        SubjectId subject,
        IReadOnlyDictionary<string, string> declared,
        string reason) =>
        (await Administration.SetPreferencesAsync(
            AccessContext.Of(administrator),
            subject,
            language: null,
            timeZone: null,
            declared,
            reason,
            TestContext.Current.CancellationToken))
        .Match<ErrorCode?>(() => null, error => error.Code);

    // A person record an organization holds, with a verified phone and an unverified
    // email, tied to the organization as the store ties it.
    private SubjectId Record(OrganizationId organization, PersonRecordState state)
    {
        var record = new HeldRecord(SubjectId.New(_randomness), organization, Noon)
        {
            State = state,
            Language = "ar",
            Name = Named<DisplayName>("Rana Haddad", DisplayName.TryParse),
            Contacts =
            {
                [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, "rana@example.test", VerifiedAt: null),
                [IdentifierKind.Phone] = new CollectedContact(IdentifierKind.Phone, "+441632960021", Noon),
            },
        };

        _records.Hold(record);
        _ties.Tie(record.Subject, organization);

        return record.Subject;
    }

    private SubjectId Account()
    {
        var subject = SubjectId.New(_randomness);

        _directory.Stands(subject, AccountState.Active);
        _directory.Registered(subject, Noon);

        return subject;
    }

    private static TName Named<TName>(string entered, TryParsing<TName> parse) =>
        parse(entered, out TName name) ? name : throw new InvalidOperationException(entered);

    private static IEnumerable<string> Members(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(property => property.PropertyType.Namespace == typeof(AccountView).Namespace
                && property.PropertyType.IsClass
                ? [property.Name, .. Members(property.PropertyType)]
                : new[] { property.Name });

    private static TValue Read<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static Error Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => throw new InvalidOperationException("It was answered."), error => error);

    private delegate bool TryParsing<TName>(string entered, out TName name);
}
