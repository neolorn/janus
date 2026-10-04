using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Recovery;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Identifiers;

/// <summary>
/// What an account may do to its own identifiers after registration: add, verify,
/// make primary, set the backup, remove with an undo, and replace where only one of
/// a kind is held (REG-IDENT-001 to REG-IDENT-010).
/// </summary>
[Trait("kind", "unit")]
public sealed class IdentifierServiceTests : IAsyncDisposable
{
    private const string Source = "198.51.100.7";
    private const string Primary = "primary@example.test";
    private const string Second = "second@example.test";
    private const string Third = "third@example.test";
    private const string Fourth = "fourth@example.test";
    private const string Number = "+441632960011";

    // IDN-ACCT-005: a Cyrillic a inside an otherwise Latin word.
    private const string Mixed = "p\u0430ypal@example.test";

    private static readonly string[] English = ["en"];

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(Source, new DeviceDescription("Firefox", "Fedora"));

    private readonly IdentifierDirectoryInMemory _directory = new();
    private readonly SettingsRestrictionInMemory _restriction = new();
    private readonly PendingVerificationStoreInMemory _pending = new();
    private readonly VerificationCodeStoreInMemory _codes = new();
    private readonly RecoveryLinkStoreInMemory _links = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly GovernedSendInMemory _notifications = new();
    private readonly SendingRestrictionsInMemory _restrictions = new();
    private readonly ThrottleLedgerInMemory _throttle = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _person;

    /// <summary>
    /// An account that holds one verified email and a password, which is what the
    /// shortest registration leaves behind.
    /// </summary>
    public IdentifierServiceTests()
    {
        _notifications.Work = _work;
        _restrictions.Work = _work;
        _pending.Work = _work;
        _codes.Work = _work;
        _directory.Pending = _pending;
        _pending.Codes = _codes;
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, English);
        _person = SubjectId.New(_randomness);
        _passwords.Hold(_person, Noon);
    }

    private IdentifierService Service =>
        new(
            _directory,
            _restriction,
            _pending,
            new VerificationCodes(_codes, _configuration, _work, _clock, _randomness),
            _notifications,
            _restrictions,
            new ThrottleService(_configuration, _throttle, _work, _events, _clock),
            Landing.Links,
            _notices,
            _sessions,
            new StepUpGuard(
                _sessions,
                _authenticators,
                _passwords,
                new PolicyResolution(_memberships, _configuration, _raises),
                _directory,
                new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock),
                _clock),
            new EnrolmentSessions(_links, _work, _clock),
            _configuration,
            _work,
            _events,
            _clock,
            _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// REG-IDENT-004 AC1: an addition is a step-up action, and a session that has
    /// not stepped up is told so rather than adding anything.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC1_AnAdditionWithoutStepUpIsRefusedAsync()
    {
        IdentifierId held = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Service.AddAsync(
                Acting,
                Stale(),
                IdentifierKind.Email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(held, Assert.Single(await HeldAsync()).Id);
    }

    /// <summary>
    /// REG-IDENT-004, REG-IDENT-006, D-178: an addition of an unreadable value and the
    /// removal of the primary are told before the step-up is asked, so a session whose
    /// proof is no longer recent hears the refusal the change meets.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_TheStepUpIsJudgedAfterEveryOtherRefusalAsync()
    {
        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.IdentifierInvalid,
            Refused(await Service.AddAsync(
                Acting,
                Stale(),
                IdentifierKind.Email,
                "not an address",
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.IdentifierPrimary,
            Refused(await Service.RemoveAsync(
                Acting,
                Stale(),
                primary,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Equal(primary, Assert.Single(await HeldAsync()).Id);
    }

    /// <summary>
    /// REG-IDENT-004 AC2: what was added waits unverified, and is in no notice set
    /// until a code settles it.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC2_AnAddedIdentifierWaitsUnverifiedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        HeldIdentifier added = (await HeldAsync()).Single(identifier =>
            string.Equals(identifier.Canonical, Second, StringComparison.Ordinal));

        Assert.False(added.IsVerified);
        Assert.DoesNotContain(added, await NoticeSetAsync());

        await VerifiedAsync(added.Id);

        Assert.True(Named(await HeldAsync(), Second).IsVerified);
        Assert.Contains(await NoticeSetAsync(), identifier => identifier.Id == added.Id);
    }

    /// <summary>
    /// REG-SESS-003 (D-166, message kinds (1)): an identifier being added is sent the
    /// message worded with a code and a link, and it carries both.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AnAddedIdentifierIsSentItsCodeAndItsLinkAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Third);

        OutboundMessage sent = _notifications.Mail.Single(one => one.Destination.Canonical == Third);

        Assert.Equal(MessageKind.VerificationLink, sent.Message);
        Assert.Equal(["code", "link"], sent.Values.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// REG-IDENT-004 AC3: every member of the set as it stands hears of the
    /// addition, once each.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC3_TheNoticeSetHearsOfTheAdditionOnceAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Email, Second);

        await AddedAsync(Third);

        string[] told = [.. _notifications.Mail
            .Where(sent => sent.Message is MessageKind.IdentifierAdded)
            .Select(sent => sent.Destination.Canonical)];

        Assert.Equal([Primary, Second], [.. told.Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// REG-IDENT-004 AC5: an add of a value no account holds writes no identifier until
    /// it verifies. Until then the account lists it as unverified, under the identifier
    /// the verified identifier then keeps.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC5_AnAddWritesNoIdentifierUntilItVerifiesAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        IdentifierId staged = Assert.Single(_pending.All).Identifier;
        HeldIdentifier listed = Named(await HeldAsync(), Second);

        Assert.Equal(staged, listed.Id);
        Assert.False(listed.IsVerified);
        Assert.True(listed.IsPending);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(staged);

        HeldIdentifier written = Named(await HeldAsync(), Second);

        Assert.Equal(staged, written.Id);
        Assert.True(written.IsVerified);
        Assert.False(written.IsPending);
        Assert.Empty(_pending.All);
        Assert.Equal(_person, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC4, REG-SESS-005: the notice to the holder of a number another
    /// account tries to add spends the number's window only where its send is admitted.
    /// One a restriction refused leaves the add as it would have been and the window
    /// unmarked; one admitted marks it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC4_AHoldersNoticeARestrictionRefusedSpendsNoWindowAsync()
    {
        const string otherNumber = "+441632960012";

        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(SubjectId.New(_randomness), IdentifierKind.Phone, Number);
        _ = _directory.Verified(SubjectId.New(_randomness), IdentifierKind.Phone, otherNumber);
        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _notifications.RefusedChannel = SendKind.Sms;

        Result refused = await Service.AddAsync(
            Acting,
            Stepped(),
            IdentifierKind.Phone,
            Number,
            Source,
            TestContext.Current.CancellationToken);
        _notifications.Refusal = null;
        Result admitted = await Service.AddAsync(
            Acting,
            Stepped(),
            IdentifierKind.Phone,
            otherNumber,
            Source,
            TestContext.Current.CancellationToken);

        Accepted(refused);
        Accepted(admitted);
        Assert.Equal(otherNumber, Assert.Single(_notifications.Texts).Destination.Canonical);
        Assert.Equal(otherNumber, Assert.Single(_notices.Told).Destination);
    }

    /// <summary>
    /// REG-IDENT-004 AC5, AC3, API-CONV-005: an add of a value another account holds is
    /// staged and listed as one of a fresh value is, and the account's own notice set
    /// hears of it alike. No code is sent, the ask is counted as its message would be,
    /// and the holder is told.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC5_AnAddOfAHeldValueIsStagedAndListedAlikeAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        await AddedAsync(Third);

        PendingVerification staged = Assert.Single(_pending.All);
        HeldIdentifier listed = Named(await HeldAsync(), Third);

        Assert.Equal(staged.Identifier, listed.Id);
        Assert.False(listed.IsVerified);
        Assert.Null(staged.Staged.Link);
        Assert.False(Outstanding(staged.Identifier).IsAnswerable());
        Assert.Equal(Third, Assert.Single(_restrictions.Drawn).Destination.Canonical);
        Assert.Equal(RestrictionPurpose.Verification, _restrictions.Drawn[0].Purpose);
        Assert.Equal(
            [MessageKind.AccountExists, MessageKind.IdentifierAdded],
            _notifications.Mail
                .Select(sent => sent.Message)
                .OrderBy(kind => kind.ToString(), StringComparer.Ordinal));
        Assert.Equal(
            Primary,
            _notifications.Mail.Single(sent => sent.Message is MessageKind.IdentifierAdded).Destination.Canonical);
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Third,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 AC6: a kind's identifiers and its pending adds together count
    /// toward its maximum, and an abandoned add counts no longer.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC6_APendingAddCountsTowardTheMaximumUntilItIsAbandonedAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        Accepted(await Service.AbandonAsync(link, TestContext.Current.CancellationToken));

        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);

        await AddedAsync(Third);

        Assert.Equal(Third, Assert.Single(_pending.All).Staged.Canonical);
    }

    /// <summary>
    /// REG-IDENT-004 AC7: of two accounts that add one value no account holds, the one
    /// that verifies first holds it. The second's right code writes nothing and is
    /// answered <c>auth.code.expired</c>, its unit of work rolled back and its pending
    /// add left listed for the sweep.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC7_TheSecondOfTwoAccountsToVerifyOneValueIsAnsweredExpiredAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        await AddedAsync(Fourth);

        IdentifierId mine = Assert.Single(_pending.All).Identifier;

        Accepted(await Service.AddAsync(
            AccessContext.Of(other),
            Stepped(other),
            IdentifierKind.Email,
            Fourth,
            Source,
            TestContext.Current.CancellationToken));

        IdentifierId theirs = _pending.All.Single(pending => pending.Subject == other).Identifier;

        Assert.True(Outstanding(theirs).IsAnswerable());

        Accepted(await Service.VerifyAsync(
            AccessContext.Of(other),
            Stepped(other),
            theirs,
            Code(theirs),
            Source,
            TestContext.Current.CancellationToken));

        string code = Code(mine);

        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                mine,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Fourth,
            TestContext.Current.CancellationToken));
        Assert.Equal(mine, Assert.Single(_pending.All).Identifier);
        Assert.False(Named(await HeldAsync(), Fourth).IsVerified);
        Assert.DoesNotContain(
            _events.Published,
            raised => raised is IdentifierAdded added && added.Subject == _person);
    }

    /// <summary>
    /// REG-IDENT-004: a press of the add's link from the browser that staged it judges
    /// the value again as the right code does, so a value held since writes nothing and
    /// is answered <c>auth.code.expired</c>.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_APressForAValueHeldSinceTheAddWritesNothingAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        SessionId browser = Stepped();

        Accepted(await Service.AddAsync(
            Acting,
            browser,
            IdentifierKind.Email,
            Fourth,
            Source,
            TestContext.Current.CancellationToken));

        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();
        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Fourth);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.False(Named(await HeldAsync(), Fourth).IsVerified);
        Assert.True(Outstanding(Assert.Single(_pending.All).Identifier).IsAnswerable());
    }

    /// <summary>
    /// REG-IDENT-004, CONV-DESIGN-003: an add of a value the account holds already
    /// writes nothing, so its unit of work is rolled back and it is answered as any
    /// other add.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AnAddOfAValueTheAccountHoldsIsRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Email, Second);

        SessionId session = Stepped();

        _work.Reset();

        Accepted(await Service.AddAsync(
            Acting,
            session,
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_pending.All);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// REG-SESS-005 AC5, AUTH-FACT-004: a code presented for an add of a value another
    /// account holds is answered as a wrong code for a value no account holds:
    /// <c>auth.code.invalid</c> for each try up to the cap, each counted and committed,
    /// then <c>auth.code.expired</c>. No code verifies it.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC5_ACodeForAHeldValueAtAnAddIsAnsweredAsAWrongOneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        await AddedAsync(Third);

        IdentifierId staged = Assert.Single(_pending.All).Identifier;
        SessionId browser = Stepped();

        for (int tried = 0; tried < Settings.CodeVerificationAttempts.Default; tried++)
        {
            Waited();

            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refused(await Service.VerifyAsync(
                    Acting,
                    browser,
                    staged,
                    "000000",
                    Source,
                    TestContext.Current.CancellationToken)));
        }

        Waited();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                browser,
                staged,
                "000000",
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(Named(await HeldAsync(), Third).IsVerified);
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Third,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC14: an ask of a code for a held value at an add is counted
    /// against the restrictions as its message would be and refused by them alike. The
    /// refusal is <c>auth.restriction.exceeded</c> and nothing is staged.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC14_AnAddOfAHeldValueTheRestrictionsRefuseStagesNothingAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        SessionId session = Stepped();

        _restrictions.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refused(await Service.AddAsync(
                Acting,
                session,
                IdentifierKind.Email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_pending.All);
        Assert.Empty(_codes.All);
        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC14, REG-IDENT-004: asking again for an add of a held value is
    /// a resend on the same pending verification, counted again as its message would be.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC14_AResendForAHeldValueIsCountedAgainOnTheSameAddAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        await AddedAsync(Third);
        await AddedAsync(Third);

        Assert.Equal(2, _restrictions.Drawn.Count);
        Assert.False(Outstanding(Assert.Single(_pending.All).Identifier).IsAnswerable());
        Assert.Single(_notifications.Mail, sent => sent.Message is MessageKind.IdentifierAdded);
    }

    /// <summary>
    /// REG-IDENT-005 AC1: the primary and the backup setting are changed in an
    /// ordinary session, and the set as it was hears of it.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_005_AC1_ThePrimaryAndTheBackupNeedNoStepUpAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.MakePrimaryAsync(
            Acting,
            second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.True(Named(await HeldAsync(), Second).IsPrimary);

        Accepted(await Service.SetBackupAsync(
            Acting,
            IdentifierKind.Email,
            BackupChoice.PrimaryOnly,
            named: null,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Assert.Single(await NoticeSetAsync()).Canonical);
    }

    /// <summary>
    /// REG-IDENT-005 AC2: an identifier the account has not proved does not become
    /// the one everything is sent to; the refusal is a failed precondition and
    /// nothing changes.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_005_AC2_AnUnverifiedIdentifierIsNotMadePrimaryAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        int told = _notifications.Mail.Count;

        Assert.Equal(
            ErrorCodes.IdentifierUnverified,
            Refused(await Service.MakePrimaryAsync(
                Acting,
                Named(await HeldAsync(), Second).Id,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.True(Named(await HeldAsync(), Primary).IsPrimary);
        Assert.False(Named(await HeldAsync(), Second).IsPrimary);
        Assert.Equal(told, _notifications.Mail.Count);
        Assert.DoesNotContain(_events.Published, raised => raised is IdentifierPrimaryChanged);
    }

    /// <summary>
    /// REG-IDENT-002, REG-IDENT-005 AC2: the backup setting names no identifier the
    /// account has not proved; the refusal is a failed precondition and the set
    /// stays as it was.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_002_AnUnverifiedIdentifierIsNotNamedTheBackupAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        IReadOnlyList<HeldIdentifier> before = await NoticeSetAsync();
        int told = _notifications.Mail.Count;

        Assert.Equal(
            ErrorCodes.IdentifierUnverified,
            Refused(await Service.SetBackupAsync(
                Acting,
                IdentifierKind.Email,
                BackupChoice.Named,
                Named(await HeldAsync(), Second).Id,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            before.Select(identifier => identifier.Id),
            (await NoticeSetAsync()).Select(identifier => identifier.Id));
        Assert.Equal(told, _notifications.Mail.Count);
    }

    /// <summary>
    /// REG-IDENT-006 AC1: a removal is a step-up action, the primary is not
    /// removable, and neither is the last of a kind the account must hold.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC1_RemovalNeedsStepUpAndSparesThePrimaryAsync()
    {
        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Service.RemoveAsync(
                Acting,
                Stale(),
                second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.IdentifierPrimary,
            Refused(await Service.RemoveAsync(
                Acting,
                Stepped(),
                primary,
                Source,
                TestContext.Current.CancellationToken)));

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        // The last of a kind is the primary of that kind, so the account is told
        // which rule refused it and the account still holds the address.
        Assert.Equal(
            ErrorCodes.IdentifierPrimary,
            Refused(await Service.RemoveAsync(
                Acting,
                Stepped(),
                primary,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(primary, Assert.Single(await HeldAsync()).Id);
    }

    /// <summary>
    /// REG-IDENT-006 AC1, CONV-DESIGN-003 AC6: an address made primary while the removal
    /// waited for the set's lock is judged as it now stands, so it is spared.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC1_AnAddressMadePrimaryMeanwhileIsSparedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        _directory.Holding = subject =>
            _directory.PromoteAsync(subject, second, TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.IdentifierPrimary,
            Refused(await Service.RemoveAsync(
                Acting,
                Stepped(),
                second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.True(Named(await HeldAsync(), Second).IsPrimary);
        Assert.DoesNotContain(_events.Published, raised => raised is IdentifierRemoved);
    }

    /// <summary>
    /// REG-MAIL-001 AC5: the personal email a membership keeps is not made primary,
    /// removed or replaced by the person while the membership lasts.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC5_ThePersonalEmailStaysAsTheMembershipKeepsItAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId personal = _directory.Verified(_person, IdentifierKind.Email, Second, isPersonal: true);

        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.MakePrimaryAsync(
                Acting,
                personal,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.RemoveAsync(
                Acting,
                Stepped(),
                personal,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.ReplaceAsync(
                Acting,
                Stepped(),
                personal,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
    }

    /// <summary>
    /// REG-IDENT-006 AC2: the value stops resolving at once, the undo brings it
    /// back inside the window, and the same link is refused after it.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC2_TheUndoRestoresInsideTheWindowAndNotAfterAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(_person, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        _clock.Advance(Settings.IdentifierChangeCoolingOff.Default + TimeSpan.FromMinutes(1));

        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(Undo(), Source, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-IDENT-006 AC3: the address that left is told with nothing it can act on,
    /// and the undo goes to the channels that remain.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC3_TheRemovedAddressIsToldWithNoLinkAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        OutboundMessage left = _notifications.Mail.Single(sent =>
            string.Equals(sent.Destination.Canonical, Second, StringComparison.Ordinal));
        OutboundMessage kept = _notifications.Mail.Single(sent =>
            string.Equals(sent.Destination.Canonical, Primary, StringComparison.Ordinal));

        Assert.Equal(MessageKind.IdentifierDetached, left.Message);
        Assert.Empty(left.Values);
        Assert.Equal(MessageKind.IdentifierRemoved, kept.Message);
        Assert.NotEmpty(kept.Token());

        Assert.NotNull(await _directory.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 (D-187): a removal naming a pending add ends its pending
    /// verification as an abandon does. It needs the step-up a removal needs, and it
    /// leaves no undo, reservation, notice, ended session or event.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_ARemovalNamingAPendingAddEndsItAsAnAbandonDoesAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        IdentifierId staged = Assert.Single(_pending.All).Identifier;

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Service.RemoveAsync(
                Acting,
                Stale(),
                staged,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Single(_pending.All);

        SessionId elsewhere = Stepped();
        int told = _notifications.Sent.Count;
        int raised = _events.Published.Count;

        _work.Reset();
        _directory.Locked.Clear();

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            staged,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Empty(_pending.All);
        Assert.Empty(_codes.All);
        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
        Assert.Equal(told, _notifications.Sent.Count);
        Assert.Equal(raised, _events.Published.Count);
        Assert.Empty(_directory.Locked);
        Assert.Null(await _directory.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
        Assert.Null((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// REG-IDENT-006 AC7: the verification of another account's add of a value,
    /// presented once that value's removal has committed, is answered
    /// <c>auth.code.expired</c> and writes nothing, and the undo then restores the value
    /// to the account it was removed from.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC7_AnAddVerifiedOnceTheValueIsReservedWritesNothingAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Fourth);

        IdentifierId staged = Assert.Single(_pending.All).Identifier;
        string code = Code(staged);
        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);
        IdentifierId theirs = _directory.Verified(other, IdentifierKind.Email, Fourth);

        Accepted(await Service.RemoveAsync(
            AccessContext.Of(other),
            Stepped(other),
            theirs,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                staged,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Fourth,
            TestContext.Current.CancellationToken));

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Fourth,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 AC5: a removed address stays reserved while the undo can still
    /// restore it, and another account adding it is answered as for a held value: the
    /// add stages a verification no code answers, no code is sent, nobody who holds or
    /// held the value is told, the ask is counted as its message would be, and the undo
    /// then restores the value.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC5_AnAddOfAReservedValueStagesAVerificationNoCodeAnswersAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();
        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        int told = _notifications.Sent.Count;

        Accepted(await Service.AddAsync(
            AccessContext.Of(other),
            Stepped(other),
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        PendingVerification staged = Assert.Single(_pending.All);

        Assert.Equal(Second, staged.Staged.Canonical);
        Assert.Null(staged.Staged.Link);
        Assert.False(Outstanding(staged.Identifier).IsAnswerable());
        Assert.Equal(Second, Assert.Single(_restrictions.Drawn).Destination.Canonical);

        // What went out is the notice the adding account's own set hears of every
        // addition, and nothing to the value or about it (REG-SESS-005).
        OutboundMessage heard = Assert.Single(_notifications.Sent.Skip(told));

        Assert.Equal(MessageKind.IdentifierAdded, heard.Message);
        Assert.Equal(Third, heard.Destination.Canonical);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(_person, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 AC5: after the undo window the same attempt stages a verification
    /// a sent code answers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC5_AfterTheWindowTheSameAddIsSentACodeThatAnswersAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        _clock.Advance(Settings.IdentifierChangeCoolingOff.Default + TimeSpan.FromMinutes(1));

        Accepted(await Service.AddAsync(
            AccessContext.Of(other),
            Stepped(other),
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        PendingVerification staged = Assert.Single(_pending.All);

        Assert.True(Outstanding(staged.Identifier).IsAnswerable());
        Assert.NotNull(staged.Staged.Link);
        Assert.Empty(_restrictions.Drawn);

        Accepted(await Service.VerifyAsync(
            AccessContext.Of(other),
            Stepped(other),
            staged.Identifier,
            Code(staged.Identifier),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-005, CONV-DESIGN-003: an addition judges the value under its lock, and
    /// its verification writes under it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_005_AnAdditionAndItsVerificationLockTheValueAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);
        await VerifiedAsync(Named(await HeldAsync(), Second).Id);

        Assert.Equal(
            [(IdentifierKind.Email, Second), (IdentifierKind.Email, Second)],
            _directory.Locked);
    }

    /// <summary>
    /// REG-SESS-005: a value another account took while the addition waited for the
    /// value's lock is judged held under that lock, so the add is staged with a record
    /// no code answers, nothing is sent to the value and no identifier is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_005_AValueTakenWhileTheAdditionWaitedForItsLockIsJudgedHeldAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        var other = SubjectId.New(_randomness);

        _directory.Locking = values =>
        {
            _directory.Locking = null;
            _ = _directory.Verified(other, IdentifierKind.Email, Second);

            return ValueTask.CompletedTask;
        };

        await AddedAsync(Second);

        PendingVerification staged = Assert.Single(_pending.All);

        Assert.False(Outstanding(staged.Identifier).IsAnswerable());
        Assert.DoesNotContain(
            _notifications.Mail,
            sent => sent.Message is MessageKind.VerificationLink);
        Assert.False(Named(await HeldAsync(), Second).IsVerified);
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006: a removal reserves the value under its lock, and the undo writes
    /// it back under the same lock.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_ARemovalAndItsUndoLockTheValueAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));
        Accepted(await Service.UndoAsync(Undo(), Source, TestContext.Current.CancellationToken));

        Assert.Equal(
            [(IdentifierKind.Email, Second), (IdentifierKind.Email, Second)],
            _directory.Locked);
    }

    /// <summary>
    /// REG-IDENT-007: the swap writes the new value and reserves the displaced one,
    /// each under its lock, both asked for at once so they are taken in one order.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_007_TheSwapLocksTheNewValueAndTheDisplacedOneAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));
        await VerifiedAsync(email);

        Assert.Equal(
            [(IdentifierKind.Email, Second), (IdentifierKind.Email, Second), (IdentifierKind.Email, Primary)],
            _directory.Locked);
    }

    /// <summary>
    /// REG-IDENT-006 AC8: an account that adds again a value it removed and verifies
    /// it within the window ends the value's reservation, and the undo link is then
    /// answered as one past its window.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC8_AValueAddedAgainAndVerifiedLeavesItsUndoPastItsWindowAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        await AddedAsync(Second);
        await VerifiedAsync(Named(await HeldAsync(), Second).Id);

        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken)));
        Assert.True(Named(await HeldAsync(), Second).IsVerified);
        Assert.Null(await _directory.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 AC8: an account that replaces back to a value it replaced, and
    /// verifies it within the window, ends that value's reservation, and the first
    /// replace's undo link is then answered as one past its window.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC8_AReplaceBackLeavesTheFirstUndoPastItsWindowAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));
        await VerifiedAsync(email);

        string undo = _notifications.Texts.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Token();

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Primary,
            Source,
            TestContext.Current.CancellationToken));
        await VerifiedAsync(email);

        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken)));
        Assert.Equal(Primary, Named(await HeldAsync(), Primary).Canonical);
        Assert.Equal(_person, await _directory.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006, REG-IDENT-004: an undo pressed while the account's own add of the
    /// removed value is pending restores the value and leaves the add as it stands. The
    /// add's code then writes nothing and is answered <c>auth.code.expired</c>, the
    /// account holds the value once, and the add is left to the sweep.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AnUndoWhileTheValueIsAddedAgainLeavesTheAddToTheSweepAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        await AddedAsync(Second);

        IdentifierId again = Assert.Single(_pending.All).Identifier;
        string code = Code(again);

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                again,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);

        HeldIdentifier restored = (await HeldAsync()).Single(identifier =>
            identifier.IsVerified
            && string.Equals(identifier.Canonical, Second, StringComparison.Ordinal));

        Assert.Equal(second, restored.Id);
        Assert.Equal(again, Assert.Single(_pending.All).Identifier);
        Assert.True(Outstanding(again).IsAnswerable());
    }

    /// <summary>
    /// REG-IDENT-006 AC9: an account at its kind's maximum that removes an email and
    /// stages a pending add is admitted its undo, since an undo counts verified
    /// identifiers alone. The pending add's right code then finds the verified emails
    /// filling the kind and is refused <c>identity.identifier.maximum</c>: nothing is
    /// written, its unit of work is rolled back, and the add stays listed for the sweep
    /// or an abandon.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC9_AnUndoIsAdmittedBesideAPendingAddWhoseVerificationIsThenRefusedAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        await AddedAsync(Third);

        IdentifierId third = Assert.Single(_pending.All).Identifier;
        string code = Code(third);

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        _work.Reset();

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                third,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(2, (await HeldAsync()).Count(identifier => identifier.IsVerified));
        Assert.Null(await _directory.OwnerAsync(IdentifierKind.Email, Third, TestContext.Current.CancellationToken));
        Assert.Equal(third, Assert.Single(_pending.All).Identifier);
        Assert.True(Outstanding(third).IsAnswerable());
    }

    /// <summary>
    /// REG-IDENT-004: a press of a pending add's link is judged against the maximum as
    /// its code is: where the account's verified identifiers already fill the kind it
    /// writes nothing and is refused <c>identity.identifier.maximum</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_APressForAnAddWhereVerifiedIdentifiersFillTheKindIsRefusedTheMaximumAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        SessionId browser = Stepped();

        Accepted(await Service.RemoveAsync(Acting, browser, second, Source, TestContext.Current.CancellationToken));

        string undo = Undo();

        Accepted(await Service.AddAsync(
            Acting,
            browser,
            IdentifierKind.Email,
            Third,
            Source,
            TestContext.Current.CancellationToken));

        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        _work.Reset();

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Null(await _directory.OwnerAsync(IdentifierKind.Email, Third, TestContext.Current.CancellationToken));
        Assert.Equal(Third, Assert.Single(_pending.All).Staged.Canonical);
    }

    /// <summary>
    /// REG-IDENT-006 AC9: an undo is refused <c>identity.identifier.maximum</c> where
    /// the account's verified identifiers of the kind already fill it. Nothing is put
    /// back and the removal stands, so the same link restores the value once the
    /// account holds one fewer.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC9_AnUndoIsRefusedWhereVerifiedIdentifiersFillTheKindAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        await AddedAsync(Third);

        IdentifierId third = Assert.Single(_pending.All).Identifier;

        await VerifiedAsync(third);

        _work.Reset();

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Null(await _directory.OwnerAsync(IdentifierKind.Email, Second, TestContext.Current.CancellationToken));

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            third,
            Source,
            TestContext.Current.CancellationToken));
        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(
            _person,
            await _directory.OwnerAsync(IdentifierKind.Email, Second, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006: an undo judged under the set's lock is refused the maximum where
    /// an add verified while it waited, and its unit of work is rolled back.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AnUndoRefusedTheMaximumUnderTheLockIsRolledBackAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        _directory.Holding = locked =>
        {
            _ = _directory.Verified(locked, IdentifierKind.Email, Third);

            return ValueTask.CompletedTask;
        };
        _work.Reset();

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Null(await _directory.OwnerAsync(IdentifierKind.Email, Second, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006: pending adds never refuse an undo, and neither does an identifier
    /// the account holds unverified: with the kind's verified identifiers one under the
    /// maximum, the undo restores the value beside both.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AnUndoCountsVerifiedIdentifiersAloneAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        await AddedAsync(Third);
        _ = _directory.Verified(_person, IdentifierKind.Email, Fourth);
        _ = await _directory.UnverifyAsync(_person, Fourth, TestContext.Current.CancellationToken);

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(
            _person,
            await _directory.OwnerAsync(IdentifierKind.Email, Second, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004: pending adds are not counted at an add's verification, so two
    /// pending adds that together fill the kind each verify.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_TwoPendingAddsDoNotRefuseEachOthersVerificationAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 3);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);
        await AddedAsync(Third);

        await VerifiedAsync(Named(await HeldAsync(), Second).Id);
        await VerifiedAsync(Named(await HeldAsync(), Third).Id);

        Assert.Equal(3, (await HeldAsync()).Count(identifier => identifier.IsVerified));
        Assert.Empty(_pending.All);
    }

    /// <summary>
    /// REG-IDENT-006, REG-IDENT-007: the undo of a replace moves the displaced value
    /// back onto the identifier that stands, which adds none to the kind, so the
    /// maximum of one does not refuse it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_TheUndoOfAReplaceIsNotRefusedTheMaximumAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        string undo = _notifications.Texts.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Token();

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.Equal(Primary, Assert.Single(await HeldAsync(), identifier => identifier.Kind is IdentifierKind.Email).Canonical);
    }

    /// <summary>
    /// REG-IDENT-006 AC10: an identifier replaced twice within the window, through three
    /// distinct values, stands behind two removals. Both displaced values stay reserved
    /// to the account, and each undo link restores the value its own removal holds.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC10_AnIdentifierReplacedTwiceStandsBehindTwoRemovalsAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);
        string first = await ReplacedAsync(email, Second);
        string second = await ReplacedAsync(email, Third);

        int standing = _directory.RemovalsOf(email);
        SubjectId? primary = await ReservedAsync(Primary);
        SubjectId? replaced = await ReservedAsync(Second);
        Accepted(await Service.UndoAsync(second, Source, TestContext.Current.CancellationToken));
        string afterSecond = Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical;
        Accepted(await Service.UndoAsync(first, Source, TestContext.Current.CancellationToken));
        string afterFirst = Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical;

        Assert.Equal(2, standing);
        Assert.Equal(_person, primary);
        Assert.Equal(_person, replaced);
        Assert.Equal(Second, afterSecond);
        Assert.Equal(Primary, afterFirst);
    }

    /// <summary>
    /// REG-IDENT-006 AC10: the undo of the first of two replaces moves its own value
    /// back and leaves the second removal as it stands, so the second value is still
    /// reserved and its link still good, and the first link is spent.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC10_AnUndoLeavesTheOtherRemovalOfItsIdentifierAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);
        string first = await ReplacedAsync(email, Second);
        string second = await ReplacedAsync(email, Third);

        Accepted(await Service.UndoAsync(first, Source, TestContext.Current.CancellationToken));

        Assert.Equal(Primary, Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical);
        Assert.Null(await ReservedAsync(Primary));
        Assert.Equal(_person, await ReservedAsync(Second));
        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(first, Source, TestContext.Current.CancellationToken)));
        Accepted(await Service.UndoAsync(second, Source, TestContext.Current.CancellationToken));
        Assert.Equal(Second, Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical);
    }

    /// <summary>
    /// REG-IDENT-006 (D-189): an undo onto an identifier that holds another value
    /// displaces that value as a replace does. The displaced value is reserved to the
    /// account behind a removal of its own, under both values' locks taken together,
    /// and its undo goes to the channels the account still has and restores it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AnUndoOntoAStandingIdentifierDisplacesTheValueItHoldsAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);
        string first = await ReplacedAsync(email, Second);
        int told = _notifications.Texts.Count(sent => sent.Message is MessageKind.IdentifierRemoved);
        _directory.Locked.Clear();

        Accepted(await Service.UndoAsync(first, Source, TestContext.Current.CancellationToken));

        Assert.Equal([(IdentifierKind.Email, Primary), (IdentifierKind.Email, Second)], _directory.Locked);
        Assert.Equal(Primary, Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical);
        Assert.Equal(_person, await ReservedAsync(Second));
        Assert.Equal(1, _directory.RemovalsOf(email));
        Assert.Equal(told + 1, _notifications.Texts.Count(sent => sent.Message is MessageKind.IdentifierRemoved));
        Assert.DoesNotContain(_notifications.Mail, sent => sent.Message is MessageKind.IdentifierRemoved);
        Accepted(await Service.UndoAsync(
            _notifications.Texts.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Token(),
            Source,
            TestContext.Current.CancellationToken));
        Assert.Equal(Second, Assert.Single(await HeldAsync(), identifier => identifier.Id == email).Canonical);
        Assert.Equal(_person, await ReservedAsync(Primary));
    }

    /// <summary>
    /// REG-IDENT-006 (D-189): an undo that moves a value back onto a standing identifier
    /// adds none to the kind, so it is not refused for the maximum even where a second
    /// replace has since moved the identifier on.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AnUndoOntoAStandingIdentifierIsNeverRefusedTheMaximumAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);
        string first = await ReplacedAsync(email, Second);
        _ = await ReplacedAsync(email, Third);

        Accepted(await Service.UndoAsync(first, Source, TestContext.Current.CancellationToken));

        Assert.Equal(Primary, Assert.Single(await HeldAsync(), identifier => identifier.Kind is IdentifierKind.Email).Canonical);
        Assert.Equal(_person, await ReservedAsync(Third));
        Assert.Equal(2, _directory.RemovalsOf(email));
    }

    /// <summary>
    /// REG-IDENT-006 AC4: the sessions the account holds elsewhere end with the
    /// identifier, and the one that asked is left alone.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC4_EveryOtherSessionEndsOnRemovalAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        SessionId elsewhere = Stepped();
        SessionId asking = Stepped();

        Accepted(await Service.RemoveAsync(
            Acting,
            asking,
            second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.NotNull(await _sessions.FindAsync(asking, TestContext.Current.CancellationToken));
        Assert.NotNull((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// IDN-LIFE-008 AC1: once a replacement applies, the value that signed in is gone,
    /// so every other session of the account ends with it and the one that staged the
    /// change is kept. Until it applies, nothing ends.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_008_AC1_EveryOtherSessionEndsWhenAReplacementAppliesAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        SessionId elsewhere = Stepped();
        SessionId asking = Stepped();

        Accepted(await Service.ReplaceAsync(
            Acting,
            asking,
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Null((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);

        await VerifiedAsync(email, asking);

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
        Assert.Null((await _sessions.FindAsync(asking, TestContext.Current.CancellationToken))?.EndedAt);
        Assert.NotNull((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// IDN-LIFE-008 AC1: the session kept is the one the replacement completes under,
    /// not the one that staged it, so a code typed in another session of the account
    /// keeps that session alone and ends the staging one with the rest.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_008_AC1_AReplacementCompletedInAnotherSessionKeepsThatSessionAloneAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        SessionId elsewhere = Stepped();
        SessionId staging = Stepped();
        SessionId completing = Stepped();

        Accepted(await Service.ReplaceAsync(
            Acting,
            staging,
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email, completing);

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
        Assert.Null((await _sessions.FindAsync(completing, TestContext.Current.CancellationToken))?.EndedAt);
        Assert.NotNull((await _sessions.FindAsync(staging, TestContext.Current.CancellationToken))?.EndedAt);
        Assert.NotNull((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// IDN-LIFE-008 AC1: a replacement the displaced address's confirmation completes
    /// completes under no session of the account, so every session ends, the one that
    /// staged it and typed the new code included.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_008_AC1_AReplacementTheOldAddressConfirmsEndsEverySessionAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        SessionId elsewhere = Stepped();
        SessionId asking = Stepped();

        Accepted(await Service.ReplaceAsync(
            Acting,
            asking,
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email, asking);

        Assert.Null((await _sessions.FindAsync(asking, TestContext.Current.CancellationToken))?.EndedAt);

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Accepted(await Service.LandAsync(
            session: null,
            asked.Token(),
            press: true,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
        Assert.NotNull((await _sessions.FindAsync(asking, TestContext.Current.CancellationToken))?.EndedAt);
        Assert.NotNull((await _sessions.FindAsync(elsewhere, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// REG-IDENT-007 AC5, AUTH-ABUSE-004 AC15: the confirmation asked of the displaced
    /// address goes under the purpose of the new address's code, since the person
    /// making the change asked for it and it is no notice.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC5_TheConfirmationIsAskedUnderTheVerificationPurposeAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Assert.Equal(RestrictionPurpose.Verification, asked.Purpose);
        Assert.Equal(Primary, asked.Destination.Canonical);
    }

    /// <summary>
    /// REG-IDENT-007, AUTH-FACT-004: the confirmation asked of the displaced address is
    /// held in a verification-code record of its own, beside the new address's, living
    /// <c>code.verification.lifetime</c> from its send and holding no code.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_TheConfirmationIsHeldInARecordOfItsOwnAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        VerificationCode confirmation = Confirmation(email);

        Assert.Equal(2, _codes.All.Count);
        Assert.False(confirmation.IsAnswerable());
        Assert.Equal(Noon + Settings.CodeVerificationLifetime.Default, confirmation.ExpiresAt);
        Assert.True(Outstanding(email).IsAnswerable());
    }

    /// <summary>
    /// REG-IDENT-007 AC2, AUTH-FACT-004: the press that confirms spends the
    /// confirmation's record, so nothing of the replace is left once the swap applies.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC2_TheConfirmingPressSpendsItsRecordAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Accepted(await Service.LandAsync(
            session: null,
            asked.Token(),
            press: true,
            Source,
            TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(_codes.All).IsAnswerable());

        await VerifiedAsync(email);

        Assert.Empty(_codes.All);
        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
    }

    /// <summary>
    /// REG-IDENT-007 AC6 (D-189): a confirmation pressed after
    /// <c>code.verification.lifetime</c> from its send changes nothing and is answered
    /// <c>auth.code.expired</c>; the identifier, the verification and the confirmation's
    /// record stay as they stood, and the press is counted against its source alone,
    /// that count the one write the refusal commits.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC6_AConfirmationPressedPastItsLifetimeIsCountedAndChangesNothingAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        _clock.Advance(Settings.CodeVerificationLifetime.Default);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.LandAsync(
                session: null,
                asked.Token(),
                press: true,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.Equal(Primary, Named(await HeldAsync(), Primary).Canonical);
        Assert.Null(Waiting(email).OldConfirmedAt);
        Assert.NotNull(Waiting(email).OldLink);
        Assert.Equal(0, Confirmation(email).Attempts);
    }

    /// <summary>
    /// REG-IDENT-007, REG-SESS-003: a replace abandoned from its link leaves neither of
    /// its records standing.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AnAbandonedReplaceLeavesNoRecordAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        string link = _notifications.Mail
            .Last(sent => sent.Message is MessageKind.VerificationLink)
            .Token();

        Accepted(await Service.AbandonAsync(link, TestContext.Current.CancellationToken));

        Assert.Empty(_pending.All);
        Assert.Empty(_codes.All);
    }

    /// <summary>
    /// REG-IDENT-007 AC4 (D-166, 306): a replace left past the lifetime of its code is
    /// swept, leaving the identifier as it stood, after which a new replace of the same
    /// identifier is accepted; until then a new one is refused.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AnAbandonedReplaceIsSweptAndANewOneIsTakenAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        _clock.Advance(Settings.CodeVerificationLifetime.Default - TimeSpan.FromSeconds(1));

        Assert.Equal(0, await SweptAsync());
        Assert.Equal(
            ErrorCodes.ChangePending,
            Refused(await Service.ReplaceAsync(
                Acting,
                Stepped(),
                email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, await SweptAsync());
        Assert.Empty(_pending.All);
        Assert.Equal(Primary, Named(await HeldAsync(), Primary).Canonical);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Third,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Third, Waiting(email).Staged.Canonical);
        Assert.True(Outstanding(email).IsLive(_clock.GetUtcNow()));
    }

    /// <summary>
    /// REG-IDENT-007 AC6: a replace whose new address verified is held by the
    /// confirmation's record while that stands, and is swept once the old address has
    /// not confirmed within <c>code.verification.lifetime</c> of the confirmation's
    /// send, leaving the identifier as it stood.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC6_AReplaceTheOldAddressLeftUnconfirmedIsSweptAsItStoodAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        Assert.DoesNotContain(
            _codes.All,
            held => held.Holder.AsSpan().SequenceEqual(PendingVerification.CodeHolder(email)));

        _clock.Advance(Settings.CodeVerificationLifetime.Default - TimeSpan.FromSeconds(1));

        Assert.Equal(0, await SweptAsync());
        Assert.True(Waiting(email).Staged.IsVerified);

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, await SweptAsync());
        Assert.Empty(_pending.All);
        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 AC4 (D-166, 306): an add left past the lifetime of its code is
    /// swept with what it staged, so the account lists it no longer and holds no
    /// identifier for it, and adding the same address again sends a new code.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AnAbandonedAddLeavesNoIdentifierAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        IdentifierId abandoned = Assert.Single(_pending.All).Identifier;
        int sent = _notifications.Mail.Count(message => message.Message is MessageKind.VerificationLink);

        _clock.Advance(Settings.CodeVerificationLifetime.Default);

        Assert.Equal(1, await SweptAsync());
        Assert.Empty(_pending.All);
        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        await AddedAsync(Second);

        IdentifierId again = Assert.Single(_pending.All).Identifier;

        Assert.NotEqual(abandoned, again);
        Assert.Equal(
            sent + 1,
            _notifications.Mail.Count(message => message.Message is MessageKind.VerificationLink));
        Assert.True(Outstanding(again).IsAnswerable());
        Assert.True(Outstanding(again).IsLive(_clock.GetUtcNow()));
    }

    /// <summary>
    /// REG-IDENT-004 AC4 and AC6: an add whose code still stands survives the sweep,
    /// listed and counted, and once it is swept the account is admitted a further add.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AC6_ASweptAddCountsTowardTheMaximumNoLongerAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 2);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        _clock.Advance(Settings.CodeVerificationLifetime.Default - TimeSpan.FromSeconds(1));

        Assert.Equal(0, await SweptAsync());
        Assert.Equal(2, (await HeldAsync()).Count);
        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, await SweptAsync());

        await AddedAsync(Third);

        Assert.Equal(Third, Assert.Single(_pending.All).Staged.Canonical);
    }

    /// <summary>
    /// REG-SESS-005 AC5: an add of a value another account holds is swept when one of a
    /// value no account holds is, neither sooner nor later.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC5_AnAddOfAHeldValueIsSweptWhenAFreshOneIsAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 3);
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(SubjectId.New(_randomness), IdentifierKind.Email, Third);

        await AddedAsync(Third);
        await AddedAsync(Fourth);

        Assert.Equal(2, _pending.All.Count);

        _clock.Advance(Settings.CodeVerificationLifetime.Default - TimeSpan.FromSeconds(1));

        Assert.Equal(0, await SweptAsync());

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, await SweptAsync());
        Assert.Empty(_pending.All);
    }

    /// <summary>
    /// REG-IDENT-007 AC7: a replace whose new value an identifier has come to hold since
    /// it was staged applies no swap. The right code writes nothing and is answered
    /// <c>auth.code.expired</c>, the identifier stays as it stood, and a new replace of
    /// it is refused <c>identity.change.pending</c> while the staged one stands.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC7_AReplaceWhoseNewValueIsHeldSinceAppliesNoSwapAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        SessionId session = Stepped();

        Accepted(await Service.ReplaceAsync(
            Acting,
            session,
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        string code = Code(email);
        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Second);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                session,
                email,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(Primary, (await HeldAsync()).Single(identifier => identifier.Id == email).Canonical);
        Assert.Null((await _sessions.FindAsync(session, TestContext.Current.CancellationToken))?.EndedAt);
        Assert.DoesNotContain(_notifications.Texts, sent => sent.Message is MessageKind.IdentifierRemoved);
        Assert.Single(_pending.All);
        Assert.Equal(
            ErrorCodes.ChangePending,
            Refused(await Service.ReplaceAsync(
                Acting,
                Stepped(),
                email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-IDENT-007 AC7: a replace whose new value an undo has come to reserve to
    /// another account since it was staged applies no swap either.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC7_AReplaceWhoseNewValueIsReservedSinceAppliesNoSwapAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        string code = Code(email);
        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);
        IdentifierId theirs = _directory.Verified(other, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            AccessContext.Of(other),
            Stepped(other),
            theirs,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                email,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(Primary, (await HeldAsync()).Single(identifier => identifier.Id == email).Canonical);
        Assert.Single(_pending.All);
    }

    /// <summary>
    /// REG-SESS-005 AC5, REG-IDENT-007: a replace by a value another account holds is
    /// staged as one by a fresh value is, with a record no code answers: nothing is sent
    /// to the value, the ask is counted as its message would be, each code presented is
    /// answered <c>auth.code.invalid</c> up to the cap and <c>auth.code.expired</c>
    /// after it, and the identifier stays as it stood.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC5_ACodeForAHeldValueAtAReplaceIsAnsweredAsAWrongOneAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Third,
            Source,
            TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(_pending.All).IsReplacement);
        Assert.False(Outstanding(email).IsAnswerable());
        Assert.Equal(Third, Assert.Single(_restrictions.Drawn).Destination.Canonical);
        Assert.DoesNotContain(_notifications.Mail, sent => sent.Message is MessageKind.VerificationLink);

        SessionId browser = Stepped();

        for (int tried = 0; tried < Settings.CodeVerificationAttempts.Default; tried++)
        {
            Waited();

            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refused(await Service.VerifyAsync(
                    Acting,
                    browser,
                    email,
                    "000000",
                    Source,
                    TestContext.Current.CancellationToken)));
        }

        Waited();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                browser,
                email,
                "000000",
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Equal(Primary, (await HeldAsync()).Single(identifier => identifier.Id == email).Canonical);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC14: an ask of a code for a held value at a replace that the
    /// restrictions refuse is answered <c>auth.restriction.exceeded</c> and stages
    /// nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC14_AReplaceByAHeldValueTheRestrictionsRefuseStagesNothingAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        SessionId session = Stepped();

        _restrictions.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refused(await Service.ReplaceAsync(
                Acting,
                session,
                email,
                Third,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_pending.All);
        Assert.Empty(_codes.All);
    }

    /// <summary>
    /// REG-IDENT-007, CONV-DESIGN-003: a replace by the value the identifier already
    /// has writes nothing, so its unit of work is rolled back and it is answered as any
    /// other replace.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AReplaceByTheValueTheAccountHoldsIsRolledBackAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        SessionId session = Stepped();

        _work.Reset();

        Accepted(await Service.ReplaceAsync(
            Acting,
            session,
            email,
            Primary,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_pending.All);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// AUTH-FACT-004: the holder of a pending verification's record is the SHA-256 of
    /// its UUID's sixteen bytes in the order of RFC 9562, and the confirmation's is a
    /// holder of its own.
    /// </summary>
    [Fact]
    public void AUTH_FACT_004_TheHolderTakesTheUuidInTheOrderOfRfc9562()
    {
        var identifier = new IdentifierId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));

        byte[] ordered = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

        Assert.Equal(SHA256.HashData(ordered), PendingVerification.CodeHolder(identifier));
        Assert.NotEqual(
            PendingVerification.CodeHolder(identifier),
            PendingVerification.ConfirmationHolder(identifier));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2: a restricted account changes none of its identifiers. Adding,
    /// removing, replacing, promoting and naming a backup are each refused with the
    /// code the gate refuses a modifying action with, and nothing is staged or given up.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountChangesNoIdentifierAsync()
    {
        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);
        _restriction.Restrict(_person);

        SessionId asking = Stepped();

        Assert.Equal(ErrorCodes.Restricted, Refused(await Service.AddAsync(
            Acting,
            asking,
            IdentifierKind.Email,
            Third,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(ErrorCodes.Restricted, Refused(await Service.RemoveAsync(
            Acting,
            asking,
            second,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(ErrorCodes.Restricted, Refused(await Service.ReplaceAsync(
            Acting,
            asking,
            primary,
            Third,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(ErrorCodes.Restricted, Refused(await Service.MakePrimaryAsync(
            Acting,
            second,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(ErrorCodes.Restricted, Refused(await Service.SetBackupAsync(
            Acting,
            IdentifierKind.Email,
            BackupChoice.PrimaryOnly,
            named: null,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Empty(_pending.All);
        Assert.Equal(3, (await HeldAsync()).Count);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses an addition, a verification, a promotion, a
    /// backup setting, a removal and a replacement, each inside its unit of work, which
    /// rolls back and leaves the identifiers as they stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachIdentifierChangeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        SessionId asking = Stepped();

        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.AddAsync(
                Acting,
                asking,
                IdentifierKind.Email,
                Third,
                Source,
                cancellationToken)));

        Assert.Empty(_pending.All);
        Assert.Equal(2, (await HeldAsync()).Count);

        await AddedAsync(Third);

        IdentifierId third = Named(await HeldAsync(), Third).Id;
        string code = Code(third);
        string[] reached = [.. (await NoticeSetAsync()).Select(identifier => identifier.Canonical)];

        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.VerifyAsync(Acting, asking, third, code, Source, cancellationToken)));

        Assert.False(Named(await HeldAsync(), Third).IsVerified);
        Assert.Equal(0, Outstanding(third).Attempts);

        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.MakePrimaryAsync(Acting, second, Source, cancellationToken)));
        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.SetBackupAsync(
                Acting,
                IdentifierKind.Email,
                BackupChoice.PrimaryOnly,
                named: null,
                Source,
                cancellationToken)));
        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.RemoveAsync(Acting, asking, second, Source, cancellationToken)));

        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        await RestrictedSinceTheGateStepAsync(
            async () => Refused(await Service.ReplaceAsync(Acting, asking, primary, Fourth, Source, cancellationToken)));

        IReadOnlyList<HeldIdentifier> held = await HeldAsync();

        Assert.Equal(third, Assert.Single(_pending.All).Identifier);
        Assert.Equal(3, held.Count);
        Assert.False(Named(held, Second).IsPrimary);
        Assert.Equal(reached, (await NoticeSetAsync()).Select(identifier => identifier.Canonical));
        Assert.DoesNotContain(_notifications.Mail, sent => sent.Message is MessageKind.IdentifierRemoved);
    }

    /// <summary>
    /// REG-IDENT-001 AC1: the one verified email an account holds is its primary,
    /// and no removal takes it away.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_001_AC1_TheLastVerifiedEmailDoesNotLeaveTheAccountAsync()
    {
        IdentifierId only = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.IdentifierPrimary,
            Refused(await Service.RemoveAsync(
                Acting,
                Stepped(),
                only,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(1, (await _directory.HeldAsync(
            _person,
            TestContext.Current.CancellationToken)).Verified(IdentifierKind.Email));
    }

    /// <summary>
    /// REG-IDENT-001 AC2: a number another account has verified cannot be verified
    /// on this one, and the account asking is told what every other add is told.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_001_AC2_ANumberOnOneAccountDoesNotVerifyOnAnotherAsync()
    {
        var other = SubjectId.New(_randomness);

        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(other, IdentifierKind.Phone, Number);

        Accepted(await Service.AddAsync(
            Acting,
            Stepped(),
            IdentifierKind.Phone,
            Number,
            Source,
            TestContext.Current.CancellationToken));

        // What went out is the notice the holder of the number gets, not a code the
        // account asking could enter (REG-SESS-005): the add is staged with a record no
        // code answers.
        Assert.Equal(MessageKind.AccountExists, Assert.Single(_notifications.Texts).Message);

        IdentifierId staged = Assert.Single(_pending.All).Identifier;

        Assert.False(Outstanding(staged).IsAnswerable());
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                staged,
                "000000",
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Phone,
            Number,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-001 AC3: a username is not an address and is never taken by the
    /// path that takes addresses.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_001_AC3_NoUsernameIsTakenByTheIdentifierPathAsync() =>
        Assert.Equal(
            ErrorCodes.IdentifierInvalid,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Username,
                "chosen",
                Source,
                TestContext.Current.CancellationToken)));

    /// <summary>
    /// REG-IDENT-001 AC4: the key that makes the phone optional loosens downwards,
    /// which is what puts a change to it through the recorded procedure.
    /// </summary>
    [Fact]
    public void REG_IDENT_001_AC4_TheKeyThatMakesThePhoneOptionalIsALoosening()
    {
        Assert.Equal(SettingDirection.Decrease, Settings.RegistrationPhone.Loosening);
        Assert.Equal(AttributeRequirement.Required, Settings.RegistrationPhone.Default);
    }

    /// <summary>
    /// REG-IDENT-001 AC5: a principal that is not a person holds no identifier, so
    /// the path that adds one to an account refuses it.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_001_AC5_NoIdentifierIsAddedToANonHumanPrincipalAsync() =>
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.AddAsync(
                AccessContext.Of(SystemPrincipal.ForDeployment(
                    "sweeper",
                    "the scheduled sweep",
                    [SystemOperation.ExpirySweep])),
                Stepped(),
                IdentifierKind.Email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));

    /// <summary>
    /// REG-IDENT-002 AC3: the set as it stood before the change is what hears of
    /// it, so an address leaving the set is told that it did.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_002_AC3_TheSetAsItWasHearsOfTheBackupChangeAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.SetBackupAsync(
            Acting,
            IdentifierKind.Email,
            BackupChoice.PrimaryOnly,
            named: null,
            Source,
            TestContext.Current.CancellationToken));

        string[] told = [.. _notifications.Mail
            .Where(sent => sent.Message is MessageKind.IdentifierSettingsChanged)
            .Select(sent => sent.Destination.Canonical)
            .Order(StringComparer.Ordinal)];

        Assert.Equal([Primary, Second], told);
        Assert.Equal(Primary, Assert.Single(await NoticeSetAsync()).Canonical);
    }

    /// <summary>
    /// REG-IDENT-007 AC1: where one address of a kind is all the account may hold,
    /// the change is one operation, the old address is not asked, and the undo
    /// reaches the channel that remains.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC1_TheSwapAppliesAndTheUndoGoesToTheOtherChannelAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
        Assert.DoesNotContain(
            await HeldAsync(),
            identifier => string.Equals(identifier.Canonical, Primary, StringComparison.Ordinal));
        Assert.Contains(_notifications.Texts, sent => sent.Message is not MessageKind.AccountExists);
    }

    /// <summary>
    /// REG-IDENT-007 AC2: with nothing else to undo a hostile change, the address
    /// being displaced confirms before the swap applies.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC2_WithNoOtherChannelTheOldAddressConfirmsAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        Assert.Equal(Primary, Named(await HeldAsync(), Primary).Canonical);

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Accepted(await Service.LandAsync(
            session: null,
            asked.Token(),
            press: true,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
    }

    /// <summary>
    /// REG-IDENT-007 AC2, CONV-DESIGN-003 AC6: the displaced address confirms on the
    /// verification as read under its lock, so a change abandoned while the
    /// confirmation waited for it is not applied: the press opens nothing, and is answered
    /// <c>auth.code.expired</c> and counted against its source (REG-IDENT-007 AC6).
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC2_AChangeAbandonedMeanwhileIsNotAppliedAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(email);

        OutboundMessage asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        _pending.Locking = identifier =>
            _ = _pending.RemoveAsync(identifier, TestContext.Current.CancellationToken).AsTask();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.LandAsync(
                session: null,
                asked.Token(),
                press: true,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.Equal(Primary, Named(await HeldAsync(), Primary).Canonical);
    }

    /// <summary>
    /// REG-IDENT-007 AC3 and AUTH-RECOV-002: within the enrolment session an approver
    /// opened for a lost mailbox, the swap applies when the new address verifies and
    /// the displaced one is never asked.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC3_AnEnrolmentSessionSwapsOnTheNewAddressAloneAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        EnrolmentSessionId opened = Enrolling(mailboxLost: true);

        Accepted(await Service.ReplaceAsync(
            opened,
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            _notifications.Mail,
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Accepted(await Service.VerifyAsync(
            opened,
            email,
            Code(email),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
    }

    /// <summary>
    /// REG-IDENT-007 and AUTH-RECOV-002: an enrolment session opened for an account
    /// whose mailbox still answers reaches the replacement no more than a session
    /// that has not stepped up does.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AnEnrolmentSessionWithAReachableMailboxIsRefusedAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.ReplaceAsync(
                Enrolling(mailboxLost: false),
                email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-ACCT-004 AC3: an address added in fullwidth and mixed case, and a number
    /// added in Arabic-Indic digits, are held in their canonical forms beside the forms
    /// the person entered.
    /// </summary>
    /// <param name="kind">What is added.</param>
    /// <param name="entered">The form the person entered.</param>
    /// <param name="canonical">The form it is held under.</param>
    [Theory]
    [InlineData(IdentifierKind.Email, "\uFF33econd@Example.TEST", Second)]
    [InlineData(
        IdentifierKind.Phone,
        "+\u0664\u0664\u0661\u0666\u0663\u0662\u0669\u0666\u0660\u0660\u0661\u0661",
        Number)]
    public async Task IDN_ACCT_004_AC3_AnIdentifierAddedInAnotherFormIsHeldCanonicalAsync(
        IdentifierKind kind,
        string entered,
        string canonical)
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Accepted(await Service.AddAsync(
            Acting,
            Stepped(),
            kind,
            entered,
            Source,
            TestContext.Current.CancellationToken));

        HeldIdentifier added = Named(await HeldAsync(), canonical);

        Assert.Equal(kind, added.Kind);
        Assert.Equal(entered, added.Entered);
    }

    /// <summary>
    /// IDN-ACCT-004 AC3: where one of a kind is all the account may hold, an address
    /// changed to one entered in fullwidth and mixed case, and a number changed to one
    /// entered in Arabic-Indic digits, are held in their canonical forms once the
    /// change applies.
    /// </summary>
    /// <param name="kind">What is changed.</param>
    /// <param name="entered">The form the person entered.</param>
    /// <param name="canonical">The form it is held under.</param>
    [Theory]
    [InlineData(IdentifierKind.Email, "\uFF33econd@Example.TEST", Second)]
    [InlineData(
        IdentifierKind.Phone,
        "+\u0664\u0664\u0661\u0666\u0663\u0662\u0669\u0666\u0660\u0660\u0661\u0662",
        "+441632960012")]
    public async Task IDN_ACCT_004_AC3_AChangeEnteredInAnotherFormIsHeldCanonicalAsync(
        IdentifierKind kind,
        string entered,
        string canonical)
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        _configuration.Set(Settings.IdentifiersPhoneMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId phone = _directory.Verified(_person, IdentifierKind.Phone, Number);
        IdentifierId changing = kind is IdentifierKind.Email ? email : phone;

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            changing,
            entered,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(changing);

        HeldIdentifier changed = Named(await HeldAsync(), canonical);

        Assert.Equal(changing, changed.Id);
        Assert.Equal(entered, changed.Entered);
    }

    /// <summary>
    /// IDN-ACCT-005 AC3: an address whose one word mixes a Cyrillic letter into Latin
    /// is refused as an addition by the code that names the mixing, and nothing waits
    /// to be verified.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_005_AC3_AMixedAddressIsRefusedAsAnAdditionByItsOwnCodeAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.IdentifierMixedScript,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Email,
                Mixed,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
        Assert.Empty(_pending.All);
    }

    /// <summary>
    /// IDN-ACCT-005 AC3: where one address is all the account may hold, a change to
    /// one whose one word mixes a Cyrillic letter into Latin is refused by the code
    /// that names the mixing, and nothing waits to be verified.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_005_AC3_AMixedAddressIsRefusedAsAChangeByItsOwnCodeAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.IdentifierMixedScript,
            Refused(await Service.ReplaceAsync(
                Acting,
                Stepped(),
                primary,
                Mixed,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(Primary, Assert.Single(await HeldAsync()).Canonical);
        Assert.Empty(_pending.All);
    }

    /// <summary>
    /// D-148, D-188: an enrolment session that has ended reaches nothing, and what it
    /// is told is what a session that has ended is told.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_ALapsedEnrolmentSessionReachesNoReplacementAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.ReplaceAsync(
                EnrolmentSessionId.New(_clock),
                email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));
    }

    // What the account is asking as, and the sessions it asks through: one that has
    // just presented what it holds, and one whose evidence is too old for a gate.
    private AccessContext Acting => AccessContext.Of(_person);

    private SessionId Stepped() => Opened(Noon, _person);

    // Long enough for any delay the wrong tries so far have earned to lapse and short of
    // the code's lifetime, so what a test reaches is the code's own cap (AUTH-ABUSE-001).
    private void Waited() => _clock.Advance(TimeSpan.FromSeconds(30));

    /// <summary>
    /// CONV-DESIGN-003 AC5: an addition whose code the send refuses, after the identifier
    /// was staged, rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnAdditionWhoseSendIsRefusedIsRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _notifications.Refusal = Error.From(ErrorCodes.Throttled);

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// AUTH-ABUSE-006 AC4: a number added below the gateway floor is refused with the
    /// floor's code, as a restriction's refusal of its code would refuse it: nothing is
    /// staged and nothing committed.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_006_AC4_AnAdditionWhoseCodeTheFloorRefusesIsRefusedAndRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _notifications.Refusal = Error.From(ErrorCodes.SmsBalanceFloor);
        _notifications.RefusedChannel = SendKind.Sms;

        Assert.Equal(
            ErrorCodes.SmsBalanceFloor,
            Refused(await Service.AddAsync(
                Acting,
                Stepped(),
                IdentifierKind.Phone,
                Number,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_notifications.Carried);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, AUTH-FACT-004: a wrong code is counted on the verification
    /// and that count is committed alone, with no rollback.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AWrongCodeCommitsItsCountAloneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string wrong = Code(second) == "000000" ? "000001" : "000000";
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                second,
                wrong,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(1, Outstanding(second).Attempts);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, AUTH-ABUSE-001 (D-189): a code presented after it expired
    /// counts no try on its record and is counted against the source alone, and that
    /// count is committed alone, with no rollback.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnExpiredCodeCommitsItsCountAloneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        _clock.Advance(Settings.CodeVerificationLifetime.Default);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                Stepped(),
                second,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.DoesNotContain(_codes.All, held => held.Attempts > 0);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
    }

    /// <summary>
    /// REG-SESS-003 AC6, AUTH-ABUSE-001 (D-189): a code presented past its attempt cap
    /// is counted against the source alone, so the identifier is counted for the wrong
    /// codes that reached the cap and for nothing after them.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC6_ACodePastItsAttemptCapIsCountedAgainstItsSourceAloneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        string wrong = code == "000000" ? "000001" : "000000";
        SessionId browser = Stepped();

        for (int tried = 0; tried < Settings.CodeVerificationAttempts.Default; tried++)
        {
            Waited();
            _ = Refused(await Service.VerifyAsync(Acting, browser, second, wrong, Source, TestContext.Current.CancellationToken));
        }

        int counted = _throttle.Failures.Count;
        Waited();
        ErrorCode refused = Refused(await Service.VerifyAsync(Acting, browser, second, code, Source, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeExpired, refused);
        Assert.Equal(2 * Settings.CodeVerificationAttempts.Default, counted);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Failures.Skip(counted));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, AUTH-ABUSE-001 (D-189): a code whose pending verification is
    /// gone under the lock names nothing the account holds: it is answered
    /// <c>auth.code.invalid</c> and counted against the source alone, and that count is
    /// committed alone, with no rollback.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACodeForAVerificationGoneMeanwhileCommitsItsCountAloneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        _pending.Locking = identifier =>
            _ = _pending.RemoveAsync(identifier, TestContext.Current.CancellationToken).AsTask();
        _work.Reset();

        ErrorCode refused = Refused(await Service.VerifyAsync(
            Acting,
            Stepped(),
            second,
            code,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeInvalid, refused);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.DoesNotContain(await HeldAsync(), identifier => identifier.Canonical == Second);
    }

    /// <summary>
    /// AUTH-FACT-004 AC3, REG-IDENT-004: the code of an added identifier is answered
    /// from its verification-code record, so the tries are capped there and the right
    /// code after them verifies nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_TheCapEndsTheCodeOfAnAddedIdentifierAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        string wrong = code == "000000" ? "000001" : "000000";
        SessionId browser = Stepped();

        for (int tried = 0; tried < Settings.CodeVerificationAttempts.Default; tried++)
        {
            Waited();

            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refused(await Service.VerifyAsync(
                    Acting,
                    browser,
                    second,
                    wrong,
                    Source,
                    TestContext.Current.CancellationToken)));
        }

        Waited();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                Acting,
                browser,
                second,
                code,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(Named(await HeldAsync(), Second).IsVerified);
    }

    /// <summary>
    /// AUTH-FACT-004, REG-IDENT-004: the record is spent by the code that verifies the
    /// identifier, so the same code presented again is answered as a wrong one.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_TheCodeThatVerifiesAnIdentifierIsSpentAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        SessionId browser = Stepped();

        Accepted(await Service.VerifyAsync(
            Acting,
            browser,
            second,
            code,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(_codes.All);
        Assert.True(Named(await HeldAsync(), Second).IsVerified);
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                Acting,
                browser,
                second,
                code,
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, REG-IDENT-007 AC6: a press that finds its verification gone
    /// under the lock opens nothing: it is answered <c>auth.code.expired</c> and counted
    /// against its source alone, and that count is committed alone, with no rollback.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APressOnAVerificationGoneMeanwhileCommitsItsCountAloneAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        SessionId browser = Stepped();
        Accepted(await Service.AddAsync(
            Acting,
            browser,
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));
        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();
        _pending.Locking = identifier =>
            _ = _pending.RemoveAsync(identifier, TestContext.Current.CancellationToken).AsTask();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
    }

    /// <summary>
    /// REG-IDENT-004 (D-188): a resend of an add's code holds the pending verification's
    /// row while it writes: the row is locked before the code is drawn again, so the
    /// sweep passes over it and the resend in flight keeps its record.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_004_AResendOfAnAddsCodeHoldsItsRowBeforeItWritesAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        int sent = _notifications.Mail.Count(message => message.Message is MessageKind.VerificationLink);
        var held = new List<(IdentifierId Row, int Sent, bool Open)>();

        _pending.Locking = identifier => held.Add((
            identifier,
            _notifications.Mail.Count(message => message.Message is MessageKind.VerificationLink),
            _work.Open));

        await AddedAsync(Second);

        Assert.Equal([(second, sent, true)], held);
        Assert.Equal(sent + 1, _notifications.Mail.Count(message => message.Message is MessageKind.VerificationLink));
        Assert.Equal(second, Assert.Single(_pending.All).Identifier);
    }

    /// <summary>
    /// REG-SESS-003 AC6, AUTH-ABUSE-001: the codes of an account's identifier are counted
    /// and throttled as a registration's are. A refused code is counted against the
    /// source and the identifier, and while the delay stands a further code is refused
    /// with the instant it lifts, before its unit of work begins.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC6_ARefusedCodeOfAnIdentifierIsCountedAgainstTheSourceAndTheIdentifierAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string wrong = Code(second) == "000000" ? "000001" : "000000";

        for (int tried = 0; tried < Settings.AbuseThrottleThreshold.Default; tried++)
        {
            Assert.Contains(
                Refused(await Service.VerifyAsync(
                    Acting,
                    Stepped(),
                    second,
                    wrong,
                    Source,
                    TestContext.Current.CancellationToken)),
                new[] { ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired });
        }

        _work.Reset();

        Error held = (await Service.VerifyAsync(
                Acting,
                Stepped(),
                second,
                wrong,
                Source,
                TestContext.Current.CancellationToken))
            .Match(() => throw new InvalidOperationException("The code was accepted."), error => error);

        Assert.Equal(ErrorCodes.Throttled, held.Code);
        Assert.Equal(
            Noon + Settings.AbuseThrottleDelayInitial.Default,
            held.Details["retryAt"].GetDateTimeOffset());
        Assert.Equal(0, _work.Opened);
        Assert.Equal(2, _throttle.Counted.Count);
        Assert.Contains((ThrottleScope.Source, Source), _throttle.Counted);
        Assert.Contains(
            (ThrottleScope.Identifier, Convert.ToHexString(_throttle.Identify(Second))),
            _throttle.Counted);
        Assert.DoesNotContain(await HeldAsync(), identifier => identifier.Id == second && identifier.IsVerified);
    }

    /// <summary>
    /// REG-SESS-003 AC6 (D-189): a code that names no verification of the account's
    /// names no identifier: it is answered <c>auth.code.invalid</c> and counted against
    /// the source alone, as a registration counts a code for an identifier its session
    /// does not hold, that count the one write the refusal commits; while that source's
    /// delay stands a further code is refused with the instant it lifts.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC6_ACodeThatNamesNoVerificationOfTheAccountIsCountedAgainstItsSourceAsync()
    {
        IdentifierId primary = _directory.Verified(_person, IdentifierKind.Email, Primary);
        SessionId browser = Stepped();
        _work.Reset();

        ErrorCode first = Refused(await Service.VerifyAsync(Acting, browser, primary, "000000", Source, TestContext.Current.CancellationToken));
        int committed = _work.OutermostCommitted;
        int rolledBack = _work.RolledBack;

        for (int tried = 1; tried < Settings.AbuseThrottleThreshold.Default; tried++)
        {
            _ = Refused(await Service.VerifyAsync(Acting, browser, primary, "000000", Source, TestContext.Current.CancellationToken));
        }

        Error held = (await Service.VerifyAsync(Acting, browser, primary, "000000", Source, TestContext.Current.CancellationToken))
            .Match(() => throw new InvalidOperationException("The code was accepted."), error => error);

        Assert.Equal(ErrorCodes.CodeInvalid, first);
        Assert.Equal(1, committed);
        Assert.Equal(0, rolledBack);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.Equal(ErrorCodes.Throttled, held.Code);
        Assert.Equal(
            Noon + Settings.AbuseThrottleDelayInitial.Default,
            held.Details["retryAt"].GetDateTimeOffset());
        Assert.False(_work.Open);
    }

    /// <summary>
    /// REG-SESS-003 AC6 (D-189): a code that names another account's pending
    /// verification names none of the account's: it is answered and counted as one that
    /// names nothing, and the other account's verification is left as it stood.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC6_ACodeForAnotherAccountsVerificationIsCountedAgainstItsSourceAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        await AddedAsync(Second);
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        string code = Code(second);
        var other = SubjectId.New(_randomness);
        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        ErrorCode refused = Refused(await Service.VerifyAsync(
            AccessContext.Of(other),
            Stepped(other),
            second,
            code,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeInvalid, refused);
        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.Equal(0, Outstanding(second).Attempts);
        Assert.False(Named(await HeldAsync(), Second).IsVerified);
    }

    /// <summary>
    /// REG-IDENT-007 AC6, REG-SESS-003 AC6: a pressed token that opens nothing is counted
    /// against the source of the request that presents it, and against no identifier,
    /// and answered <c>auth.code.expired</c>; while that source's delay stands the press
    /// is refused with the instant it lifts; one merely opened counts nothing.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC6_APressedTokenThatOpensNothingIsCountedAgainstItsSourceAsync()
    {
        const string presenting = "203.0.113.44";
        const string nothing = "a-token-no-verification-sent";

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.LandAsync(
                session: null,
                nothing,
                press: false,
                presenting,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_throttle.Counted);

        for (int press = 0; press < Settings.AbuseThrottleThreshold.Default; press++)
        {
            Assert.Equal(
                ErrorCodes.CodeExpired,
                Refused(await Service.LandAsync(
                    session: null,
                    nothing,
                    press: true,
                    presenting,
                    TestContext.Current.CancellationToken)));
        }

        Error held = (await Service.LandAsync(
                session: null,
                nothing,
                press: true,
                presenting,
                TestContext.Current.CancellationToken))
            .Match(_ => throw new InvalidOperationException("The press was accepted."), error => error);

        Assert.Equal([(ThrottleScope.Source, presenting)], _throttle.Counted);
        Assert.Equal(ErrorCodes.Throttled, held.Code);
        Assert.Equal(
            Noon + Settings.AbuseThrottleDelayInitial.Default,
            held.Details["retryAt"].GetDateTimeOffset());
        Assert.False(_work.Open);
    }

    /// <summary>
    /// REG-IDENT-007 AC6: the press of a link whose pending verification the sweep has
    /// taken opens nothing: it is answered <c>auth.code.expired</c>, counted against its
    /// source, and changes nothing.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_AC6_APressAfterTheSweepIsAnsweredExpiredAndCountedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        SessionId browser = Stepped();
        Accepted(await Service.AddAsync(
            Acting,
            browser,
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));
        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();
        IdentifierId second = Named(await HeldAsync(), Second).Id;
        await _pending.RemoveAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));

        Assert.Equal([(ThrottleScope.Source, Source)], _throttle.Counted);
        Assert.DoesNotContain(await HeldAsync(), identifier => identifier.Canonical == Second);
    }

    /// <summary>
    /// REG-SESS-003: every press is first held to the delay of its source, the press of
    /// a link that would verify included, and a link merely opened is held to none.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_APressThatWouldVerifyIsHeldToItsSourcesDelayAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        SessionId browser = Stepped();
        Accepted(await Service.AddAsync(
            Acting,
            browser,
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));
        string link = _notifications.Mail.Last(sent => sent.Message is MessageKind.VerificationLink).Token();

        for (int press = 0; press < Settings.AbuseThrottleThreshold.Default; press++)
        {
            Assert.Equal(
                ErrorCodes.CodeExpired,
                Refused(await Service.LandAsync(
                    browser,
                    "a-token-no-verification-sent",
                    press: true,
                    Source,
                    TestContext.Current.CancellationToken)));
        }

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));
        Assert.DoesNotContain(await HeldAsync(), identifier => identifier.Canonical == Second && identifier.IsVerified);

        Accepted(await Service.LandAsync(browser, link, press: false, Source, TestContext.Current.CancellationToken));

        _clock.Advance(Settings.AbuseThrottleDelayInitial.Default);

        Accepted(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken));
        Assert.Contains(await HeldAsync(), identifier => identifier.Canonical == Second && identifier.IsVerified);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a promotion refused under the set's lock, for an identifier
    /// that left the set since it was first judged, rolls its unit of work back and
    /// commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APromotionRefusedUnderTheLockIsRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        _directory.Holding = subject =>
            _directory.DiscardAsync(subject, second, TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.IdentifierInvalid,
            Refused(await Service.MakePrimaryAsync(Acting, second, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a backup setting refused under the set's lock, for an
    /// identifier that left the set since it was first judged, rolls its unit of work
    /// back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ABackupRefusedUnderTheLockIsRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        _directory.Holding = subject =>
            _directory.DiscardAsync(subject, second, TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.IdentifierInvalid,
            Refused(await Service.SetBackupAsync(
                Acting,
                IdentifierKind.Email,
                BackupChoice.Named,
                second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: an undo that finds the value already back under the set's
    /// lock rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnUndoSpentMeanwhileIsRolledBackAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        Accepted(await Service.RemoveAsync(Acting, Stepped(), second, Source, TestContext.Current.CancellationToken));
        string undo = Undo();
        _directory.Holding = async _ =>
            await _directory.TakeBackAsync(
                OpaqueToken.Of(undo).Fingerprint(),
                _clock.GetUtcNow(),
                _clock.GetUtcNow() + Settings.IdentifierChangeCoolingOff.Default,
                Drawn(),
                TestContext.Current.CancellationToken);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a replacement whose code the send refuses, after the change
    /// was staged, rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AReplacementWhoseSendIsRefusedIsRolledBackAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);
        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _notifications.Refusal = Error.From(ErrorCodes.Throttled);

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await Service.ReplaceAsync(
                Acting,
                Stepped(),
                email,
                Second,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: the notice of a promotion that a restriction refuses fails
    /// nothing. The identifier is made primary and the change committed, with the answer
    /// it would have had.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_APromotionWhoseNoticeIsRefusedIsCommittedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Accepted(await Service.MakePrimaryAsync(
            Acting,
            second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_notifications.Carried);
        Assert.True(Named(await HeldAsync(), Second).IsPrimary);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: the notice of a backup change that a restriction refuses
    /// fails nothing. The setting is changed and committed, with the answer it would have
    /// had.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_ABackupChangeWhoseNoticeIsRefusedIsCommittedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Email, Second);

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Accepted(await Service.SetBackupAsync(
            Acting,
            IdentifierKind.Email,
            BackupChoice.PrimaryOnly,
            named: null,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_notifications.Carried);
        Assert.Equal(Primary, Assert.Single(await NoticeSetAsync()).Canonical);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: the notices of a removal that a restriction refuses fail
    /// nothing. The identifier leaves the account, its value is reserved for the undo,
    /// and the removal is committed, with the answer it would have had.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_ARemovalWhoseNoticesAreRefusedIsCommittedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);
        SessionId session = Stepped();

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Accepted(await Service.RemoveAsync(
            Acting,
            session,
            second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_notifications.Carried);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await _directory.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: the notice of an undo that a restriction refuses fails
    /// nothing. The identifier is restored and the undo committed, with the answer it
    /// would have had.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_AnUndoWhoseNoticeIsRefusedIsCommittedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        IdentifierId second = _directory.Verified(_person, IdentifierKind.Email, Second);

        Accepted(await Service.RemoveAsync(
            Acting,
            Stepped(),
            second,
            Source,
            TestContext.Current.CancellationToken));

        string undo = Undo();

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _work.Reset();

        Accepted(await Service.UndoAsync(undo, Source, TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(_person, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: where the code of an addition is admitted and the notice to
    /// the set is refused, the addition is staged and committed with the answer it would
    /// have had: the code's refusal is the one the operation answers, the notice's is
    /// not.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_AnAdditionWhoseNoticeIsRefusedIsStagedAndCommittedAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);
        SessionId session = Stepped();

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _notifications.RefusedChannel = SendKind.Email;

        Accepted(await Service.AddAsync(
            Acting,
            session,
            IdentifierKind.Phone,
            Number,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(MessageKind.VerificationLink, Assert.Single(_notifications.Carried).Message);
        Assert.Equal(Number, Assert.Single(_pending.All).Staged.Canonical);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18, REG-SESS-005 AC1: an addition of a value another account
    /// holds, whose notice to that holder a restriction refuses, is answered as it would
    /// have been. The add is staged and committed, and the holder keeps the value.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_AnAdditionOfAHeldValueWhoseNoticeIsRefusedIsAnsweredAlikeAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        var other = SubjectId.New(_randomness);

        _passwords.Hold(other, Noon);
        _ = _directory.Verified(other, IdentifierKind.Email, Third);

        SessionId session = Stepped();

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Accepted(await Service.AddAsync(
            Acting,
            session,
            IdentifierKind.Email,
            Third,
            Source,
            TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.RolledBack);
        Assert.False(Outstanding(Assert.Single(_pending.All).Identifier).IsAnswerable());
        Assert.Empty(_notifications.Carried);
        Assert.Equal(other, await _directory.OwnerAsync(
            IdentifierKind.Email,
            Third,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18: the undo notice of a swap that a restriction refuses fails
    /// nothing. The verified replacement is applied and committed, with the answer it
    /// would have had.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_ASwapWhoseNoticeIsRefusedIsAppliedAndCommittedAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);
        _ = _directory.Verified(_person, IdentifierKind.Phone, Number);

        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        SessionId session = Stepped();

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _work.Reset();

        await VerifiedAsync(email, session);

        Assert.False(_work.Open);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.Empty(_notifications.Texts);
        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
        Assert.DoesNotContain(
            await HeldAsync(),
            identifier => string.Equals(identifier.Canonical, Primary, StringComparison.Ordinal));
    }

    // AUTHZ-GATE-006 AC3: the change made with the account restricted in the moment
    // before the next unit of work begins, which is after the change's gate step. It is
    // refused as the gate refuses, the unit of work it began is rolled back and none is
    // left open; the restriction is lifted once the change has answered.
    private async ValueTask RestrictedSinceTheGateStepAsync(Func<ValueTask<ErrorCode>> change)
    {
        int rolledBack = _work.RolledBack;

        _restriction.Admitted = admitted => _work.Meanwhile = () => _restriction.Restrict(admitted);

        ErrorCode refused = await change();

        _restriction.Lift(_person);

        Assert.Equal(ErrorCodes.Restricted, refused);
        Assert.Equal(rolledBack + 1, _work.RolledBack);
        Assert.False(_work.Open);
    }

    private SessionId Stepped(SubjectId subject) => Opened(_clock.GetUtcNow(), subject);

    private SessionId Stale() =>
        Opened(Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1), _person);

    private SessionId Opened(DateTimeOffset at, SubjectId subject)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

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

    private async Task AddedAsync(string value)
    {
        Accepted(await Service.AddAsync(
            Acting,
            Stepped(),
            IdentifierKind.Email,
            value,
            Source,
            TestContext.Current.CancellationToken));
    }

    // The session an approved link opened, which is the spent link itself (D-147).
    private EnrolmentSessionId Enrolling(bool mailboxLost)
    {
        var opened = EnrolmentSessionId.New(_clock);
        var link = RecoveryLink.Issue(
            OpaqueToken.Of("the-approver-sent-this-one"),
            _person,
            RecoveryPurpose.Enrolment,
            _clock.GetUtcNow(),
            TimeSpan.FromHours(1),
            approver: SubjectId.New(_randomness),
            mailboxLost);

        link.Spend(opened, _clock.GetUtcNow());

        _links.ReplaceAsync(link, TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return opened;
    }

    // One pass of the expiry sweep over the pending verifications (OPS-OBS-003).
    private async Task<int> SweptAsync() =>
        await _pending.SweepAsync(_clock.GetUtcNow(), TestContext.Current.CancellationToken);

    private PendingVerification Waiting(IdentifierId identifier) =>
        _pending.All.Single(pending => pending.Identifier == identifier);

    private async Task VerifiedAsync(IdentifierId identifier) =>
        await VerifiedAsync(identifier, Stepped());

    private async Task VerifiedAsync(IdentifierId identifier, SessionId session)
    {
        Accepted(await Service.VerifyAsync(
            Acting,
            session,
            identifier,
            Code(identifier),
            Source,
            TestContext.Current.CancellationToken));
    }

    // A replace of single-address mode carried through its code, and the undo link the
    // remaining channel was sent for the value it displaced (REG-IDENT-007).
    private async Task<string> ReplacedAsync(IdentifierId identifier, string value)
    {
        Accepted(await Service.ReplaceAsync(
            Acting,
            Stepped(),
            identifier,
            value,
            Source,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(identifier);

        return _notifications.Texts.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Token();
    }

    private async Task<SubjectId?> ReservedAsync(string canonical) =>
        await _directory.ReservedToAsync(
            IdentifierKind.Email,
            canonical,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

    // The code is in the verification-code record it is answered from, as it is in
    // the message.
    private string Code(IdentifierId identifier) => VerificationCode.Read(Outstanding(identifier).Code);

    // The verification-code record held against a pending verification, which is the
    // holder its identifier gives (AUTH-FACT-004).
    private VerificationCode Outstanding(IdentifierId identifier) =>
        Held(PendingVerification.CodeHolder(identifier));

    // The record the displaced address's confirmation is held in (REG-IDENT-007).
    private VerificationCode Confirmation(IdentifierId identifier) =>
        Held(PendingVerification.ConfirmationHolder(identifier));

    private VerificationCode Held(byte[] holder) =>
        _codes.All.Single(held => held.Holder.AsSpan().SequenceEqual(holder));

    // The undo the remaining channels were sent, which is what the removal notice
    // carries for the deployment's template to put in its words.
    private string Undo() =>
        _notifications.Mail.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Token();

    private async Task<IReadOnlyList<HeldIdentifier>> HeldAsync() =>
        (await _directory.HeldAsync(_person, TestContext.Current.CancellationToken)).All;

    private async Task<IReadOnlyList<HeldIdentifier>> NoticeSetAsync() =>
        (await _directory.HeldAsync(_person, TestContext.Current.CancellationToken)).NoticeSet;

    private static HeldIdentifier Named(IEnumerable<HeldIdentifier> all, string canonical) =>
        all.Single(identifier =>
            string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal));

    private static void Accepted<TValue>(Result<TValue> outcome) =>
        _ = outcome.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private static ErrorCode Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match<ErrorCode>(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);
}
