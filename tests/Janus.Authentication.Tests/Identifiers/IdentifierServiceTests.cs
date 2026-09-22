using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
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
    private const string Number = "+441632960011";

    private static readonly string[] English = ["en"];

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(Source, new DeviceDescription("Firefox", "Fedora"));

    private readonly IdentifierDirectoryInMemory _directory = new();
    private readonly PendingVerificationStoreInMemory _pending = new();
    private readonly RecoveryLinkStoreInMemory _links = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly NotificationHandlerInMemory _notifications = new();
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
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, English);
        _person = SubjectId.New(_randomness);
        _passwords.Hold(_person, Noon);

    }

    private IdentifierService Service =>
        new(
            _directory,
            _pending,
            _notifications,
            _notices,
            _sessions,
            new StepUpGuard(
                _sessions,
                _authenticators,
                _passwords,
                new PolicyResolution(_memberships, _configuration, _raises),
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
    /// the one everything is sent to.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_005_AC2_AnUnverifiedIdentifierIsNotMadePrimaryAsync()
    {
        _ = _directory.Verified(_person, IdentifierKind.Email, Primary);

        await AddedAsync(Second);

        Assert.Equal(
            ErrorCodes.IdentifierInvalid,
            Refused(await Service.MakePrimaryAsync(
                Acting,
                Named(await HeldAsync(), Second).Id,
                Source,
                TestContext.Current.CancellationToken)));
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

        SendRequest left = _notifications.Mail.Single(sent =>
            string.Equals(sent.Destination.Canonical, Second, StringComparison.Ordinal));
        SendRequest kept = _notifications.Mail.Single(sent =>
            string.Equals(sent.Destination.Canonical, Primary, StringComparison.Ordinal));

        Assert.Equal(MessageKind.IdentifierDetached, left.Message);
        Assert.Empty(left.Values);
        Assert.Equal(MessageKind.IdentifierRemoved, kept.Message);
        Assert.NotEmpty(kept.Values["token"]);

        Assert.True(await _directory.IsReservedAsync(
            IdentifierKind.Email,
            Second,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
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

        SendRequest asked = _notifications.Mail.Last(
            sent => sent.Message is MessageKind.IdentifierChangeConfirm);

        Accepted(await Service.LandAsync(
            session: null,
            asked.Values["token"],
            press: true,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Second, Named(await HeldAsync(), Second).Canonical);
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
            VerificationCode.Read(Waiting(email).Staged.Code!),
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

    private SessionId Stepped() => Opened(Noon);

    private SessionId Stale() =>
        Opened(Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1));

    private SessionId Opened(DateTimeOffset at)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _person,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
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

    private async Task VerifiedAsync(IdentifierId identifier)
    {
        PendingVerification waiting = _pending.All.Single(pending =>
            pending.Identifier == identifier);

        Accepted(await Service.VerifyAsync(
            Acting,
            identifier,
            VerificationCode.Read(waiting.Staged.Code!),
            Source,
            TestContext.Current.CancellationToken));
    }

    // The undo the remaining channels were sent, which is what the removal notice
    // carries for the deployment's template to put in its words.
    private string Undo() =>
        _notifications.Mail.Last(sent => sent.Message is MessageKind.IdentifierRemoved).Values["token"];

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
}
