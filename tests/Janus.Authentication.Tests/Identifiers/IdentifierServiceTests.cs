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

        Assert.True(await _directory.IsReservedAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 AC2: a removed address stays out of reach while the undo can
    /// still restore it, and an account offering it is answered exactly as an account
    /// offering an address another holds is answered. Once the window has run out the
    /// address is free.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC2_AReservedAddressIsAnsweredAsAHeldOneIsAsync()
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

        int told = _notifications.Sent.Count;

        Accepted(await Service.AddAsync(
            AccessContext.Of(other),
            Stepped(other),
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        // Nothing was staged, so no code can be entered against the address, and the
        // account asking cannot tell a reserved value from a held one (REG-SESS-005).
        // Nobody was told either, because a reserved value has no holder to tell.
        Assert.Empty(_pending.All);
        Assert.Equal(told, _notifications.Sent.Count);
        Assert.Null(await _directory.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        _clock.Advance(Settings.IdentifierChangeCoolingOff.Default + TimeSpan.FromMinutes(1));

        Accepted(await Service.AddAsync(
            AccessContext.Of(other),
            Stepped(other),
            IdentifierKind.Email,
            Second,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Assert.Single(_pending.All).Staged.Canonical);
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
        // account asking could enter (REG-SESS-005).
        Assert.Equal(MessageKind.AccountExists, Assert.Single(_notifications.Texts).Message);
        Assert.Empty(_pending.All);
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
    /// confirmation waited for it is not applied.
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
            ErrorCodes.CodeInvalid,
            Refused(await Service.LandAsync(
                session: null,
                asked.Token(),
                press: true,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
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
    /// D-148: an enrolment session that has lapsed reaches nothing, and what it is
    /// told says only that the token opens nothing.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_007_ALapsedEnrolmentSessionReachesNoReplacementAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        IdentifierId email = _directory.Verified(_person, IdentifierKind.Email, Primary);

        Assert.Equal(
            ErrorCodes.EnrolmentTokenInvalid,
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
    /// CONV-DESIGN-003 AC5: a code presented after it expired counts nothing, so the
    /// refusal rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnExpiredCodeIsRolledBackAsync()
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
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.DoesNotContain(_codes.All, held => held.Attempts > 0);
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
    /// CONV-DESIGN-003 AC5: a press that finds its verification gone under the lock
    /// rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APressOnAVerificationGoneMeanwhileIsRolledBackAsync()
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
            ErrorCodes.CodeInvalid,
            Refused(await Service.LandAsync(browser, link, press: true, Source, TestContext.Current.CancellationToken)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
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
        _directory.Holding = _ =>
            _directory.TakeBackAsync(second, maximum: 5, TestContext.Current.CancellationToken);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.ChangeWindowElapsed,
            Refused(await Service.UndoAsync(Undo(), Source, TestContext.Current.CancellationToken)));

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
        Assert.True(await _directory.IsReservedAsync(
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
    /// AUTH-ABUSE-004 AC18, REG-SESS-005 AC1: the notice to the holder of a value another
    /// account offers, refused by a restriction, fails nothing: the asker is answered as
    /// it would have been, nothing is staged and no unit of work is left open.
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
        Assert.Empty(_pending.All);
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

    // The code is in the verification-code record it is answered from, as it is in
    // the message.
    private string Code(IdentifierId identifier) => VerificationCode.Read(Outstanding(identifier).Code);

    // The verification-code record held against a pending verification, which is the
    // fingerprint of the identifier it names (AUTH-FACT-004).
    private VerificationCode Outstanding(IdentifierId identifier)
    {
        byte[] named = new byte[16];

        _ = identifier.Value.TryWriteBytes(named);

        byte[] holder = SHA256.HashData(named);

        return _codes.All.Single(held => held.Holder.AsSpan().SequenceEqual(holder));
    }

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
