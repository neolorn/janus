using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.BreakGlass;
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
/// What an administrator does to the standing of someone else's account: suspension,
/// which ends the account's sessions, and reactivation, which restores what it held
/// (IDN-LIFE-013, AUTH-SESS-010, IDN-AUD-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountAdministrationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere =
        new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"));

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of([]);

    private readonly AccountDirectoryInMemory _directory = new(Declared);
    private readonly EmergencyAccountInMemory _emergency = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly LifecycleLinkStoreInMemory _links = new();
    private readonly AccountAuditInMemory _audit = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _administrator;
    private readonly SubjectId _member;

    /// <summary>
    /// An administrator who holds <c>account:manage</c> in the administrative
    /// organization, and an active account with a session of its own.
    /// </summary>
    public AccountAdministrationTests()
    {
        var administering = OrganizationId.New(_clock);

        _administrative.Organization = administering;
        _administrator = SubjectId.New(_randomness);
        _member = SubjectId.New(_randomness);
        _gate.Grant(_administrator, administering, Permissions.AccountManage);
        _directory.Stands(_administrator, AccountState.Active);
        _directory.Stands(_member, AccountState.Active);
        _passwords.Hold(_administrator, Noon);
        _ = Opened(_member, Noon);
    }

    private AccountAdministration Administration =>
        new(
            new AdministrativeScope(_gate, _administrative),
            new StepUpGuard(
                _sessions,
                _authenticators,
                _passwords,
                new PolicyResolution(_memberships, _configuration, _raises),
                new IdentifierDirectoryInMemory(),
                new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock),
                _clock),
            _directory,
            _emergency,
            _sessions,
            _links,
            _configuration,
            _events,
            _audit,
            new ProfilePhotos(
                _directory,
                new SettingsRestrictionInMemory(),
                new PolicyResolution(_memberships, _configuration, _raises),
                _configuration,
                _audit,
                _work,
                codec: null,
                _clock),
            _work,
            _clock);

    private AccessContext Acting => AccessContext.Of(_administrator);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-SESS-010: the suspension ends every session of the account in the operation
    /// that suspends it, announces the administrator's suspension, and is recorded as
    /// the administrator's act on the member's account.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_SuspensionEndsTheSessionsOfTheAccountAsync()
    {
        Accepted(await SuspendAsync(_member));

        Assert.Equal(AccountState.Suspended, await StateAsync(_member));
        Assert.Equal(
            SuspensionOrigin.Administrator,
            await _directory.SuspendedByAsync(_member, TestContext.Current.CancellationToken));
        Assert.Empty(await LiveAsync(_member));

        AccountSuspended suspended = Assert.Single(_events.Of<AccountSuspended>());

        Assert.Equal(
            (SuspensionOrigin.Administrator, _member, _administrator),
            (suspended.By, suspended.Subject, suspended.Actor));
        Assert.Equal(
            new RecordedChange(AuditActions.AccountSuspended, _administrator, _member, Noon),
            Assert.Single(_audit.Administered));
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// OPS-BOOT-002: the account the break-glass session belongs to is never suspended,
    /// by any administrator, and nothing is announced for the refusal.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedAsync()
    {
        _emergency.Account = _member;

        Assert.Equal(ErrorCodes.Denied, Refused(await SuspendAsync(_member)));
        Assert.Equal(AccountState.Active, await StateAsync(_member));
        Assert.Empty(_events.Of<AccountSuspended>());
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-LIFE-013 AC1: reactivation stands the account back up as it stood, and a
    /// restriction in force when it was suspended is in force again.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AC1_ReactivationRestoresPriorAccessExactlyAsync()
    {
        var restricted = SubjectId.New(_randomness);

        _directory.Stands(restricted, AccountState.Restricted);

        Accepted(await SuspendAsync(_member));
        Accepted(await SuspendAsync(restricted));
        Accepted(await ReactivateAsync(_member));
        Accepted(await ReactivateAsync(restricted));

        Assert.Equal(AccountState.Active, await StateAsync(_member));
        Assert.Equal(AccountState.Restricted, await StateAsync(restricted));
        Assert.Null(await _directory.SuspendedByAsync(_member, TestContext.Current.CancellationToken));
        Assert.Equal(
            [_member, restricted],
            [.. _events.Of<AccountReactivated>().Select(reactivated => reactivated.Subject)]);
        Assert.Equal(
            new RecordedChange(AuditActions.AccountReactivated, _administrator, restricted, Noon),
            _audit.Administered[^1]);
    }

    /// <summary>
    /// IDN-LIFE-013: an account its owner deactivated becomes the administrator's to
    /// reactivate, and nothing is announced because it was suspended already.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_ADeactivatedAccountBecomesTheAdministratorsAsync()
    {
        _directory.Suspended(_member, SuspensionOrigin.Self);

        Accepted(await SuspendAsync(_member));

        Assert.Equal(
            SuspensionOrigin.Administrator,
            await _directory.SuspendedByAsync(_member, TestContext.Current.CancellationToken));
        Assert.Empty(_events.Of<AccountSuspended>());
        Assert.Equal(AuditActions.AccountSuspended, Assert.Single(_audit.Administered).Action);

        Accepted(await ReactivateAsync(_member));

        Assert.Equal(AccountState.Active, await StateAsync(_member));
    }

    /// <summary>
    /// IDN-LIFE-013: only an administrator's suspension is reversed here; what its owner
    /// deactivated is theirs to stand back up, and an account in no suspension has
    /// nothing to reverse. Each is a conflict with the state, named with who suspended
    /// it where it is suspended.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_OnlyAnAdministratorsSuspensionIsReactivatedAsync()
    {
        var deactivated = SubjectId.New(_randomness);

        _directory.Suspended(deactivated, SuspensionOrigin.Self);

        Error owners = Refusal(await ReactivateAsync(deactivated));
        Error active = Refusal(await ReactivateAsync(_member));

        Assert.Equal(ErrorCodes.AccountStateConflict, owners.Code);
        Assert.Equal("suspended", owners.Details["state"].GetString());
        Assert.Equal("self", owners.Details["suspendedBy"].GetString());
        Assert.Equal(ErrorCodes.AccountStateConflict, active.Code);
        Assert.Equal("active", active.Details["state"].GetString());
        Assert.False(active.Details.ContainsKey("suspendedBy"));
        Assert.Equal(AccountState.Suspended, await StateAsync(deactivated));
        Assert.Equal(
            SuspensionOrigin.Self,
            await _directory.SuspendedByAsync(deactivated, TestContext.Current.CancellationToken));
        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-LIFE-013: an account in its deletion window or erased is past suspending,
    /// which is a conflict with its state, and a subject no account bears is not found.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AnAccountBeingDeletedOrUnknownIsNotSuspendedAsync()
    {
        _directory.Deleting(_member, DeletionOrigin.Self, Noon);

        Error deleting = Refusal(await SuspendAsync(_member));

        Assert.Equal(ErrorCodes.AccountStateConflict, deleting.Code);
        Assert.Equal("deleting", deleting.Details["state"].GetString());
        Assert.Equal(ErrorCodes.AccountNotFound, Refused(await SuspendAsync(SubjectId.New(_randomness))));
        Assert.Equal(ErrorCodes.AccountNotFound, Refused(await ReactivateAsync(SubjectId.New(_randomness))));
        Assert.Equal(AccountState.Deleting, await StateAsync(_member));
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-LIFE-013: suspending an account an administrator already suspended changes
    /// nothing, so it asks nothing of the session and records nothing.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_SuspendingTwiceChangesNothingAsync()
    {
        _directory.Suspended(_member, SuspensionOrigin.Administrator);

        Accepted(await Administration.SuspendAsync(
            Acting,
            Opened(_administrator, Stale),
            _member,
            TestContext.Current.CancellationToken));

        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Administered);
        Assert.Equal(0, _work.Committed);
    }

    /// <summary>
    /// Chapter 09 section 8a: both operations are <c>account:manage</c> in the
    /// administrative organization and step-up actions, and without either the account
    /// stands as it stood.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_WithoutThePermissionOrAStepUpNothingChangesAsync()
    {
        var stranger = SubjectId.New(_randomness);

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Administration.SuspendAsync(
                AccessContext.Of(stranger),
                Opened(stranger, Noon),
                _member,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Administration.SuspendAsync(
                Acting,
                Opened(_administrator, Stale),
                _member,
                TestContext.Current.CancellationToken)));

        _directory.Suspended(_member, SuspensionOrigin.Administrator);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Administration.ReactivateAsync(
                Acting,
                Opened(_administrator, Stale),
                _member,
                TestContext.Current.CancellationToken)));
        Assert.Equal(AccountState.Suspended, await StateAsync(_member));
        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// PRIV-RIGHT-004 AC2: lifting a restriction makes the account active again, tells
    /// the subscribers, and is recorded as the administrator's act.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_004_AC2_LiftingARestrictionRestoresTheAccountAsync()
    {
        _directory.Stands(_member, AccountState.Restricted);

        Accepted(await LiftAsync(_member));

        Assert.Equal(AccountState.Active, await StateAsync(_member));
        Assert.Equal((_member, Noon), Assert.Single(_directory.Lifted));
        Assert.Equal(
            new RecordedChange(AuditActions.RestrictionLifted, _administrator, _member, Noon),
            Assert.Single(_audit.Administered));
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// PRIV-RIGHT-004: only a restriction in force is lifted here; one held while the
    /// account is suspended stays until it comes back, an account with none has nothing
    /// to lift, and without <c>account:manage</c> nothing is lifted.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_004_OnlyARestrictionInForceIsLiftedAsync()
    {
        var held = SubjectId.New(_randomness);
        var restricted = SubjectId.New(_randomness);
        var stranger = SubjectId.New(_randomness);

        _directory.Stands(held, AccountState.Restricted);
        _directory.Stands(restricted, AccountState.Restricted);
        Accepted(await SuspendAsync(held));

        Error suspended = Refusal(await LiftAsync(held));

        Assert.Equal(ErrorCodes.AccountStateConflict, suspended.Code);
        Assert.Equal("suspended", suspended.Details["state"].GetString());
        Assert.Equal("administrator", suspended.Details["suspendedBy"].GetString());
        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await LiftAsync(_member)));
        Assert.Equal(ErrorCodes.AccountNotFound, Refused(await LiftAsync(SubjectId.New(_randomness))));
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Administration.LiftRestrictionAsync(
                AccessContext.Of(stranger),
                Opened(stranger, Noon),
                restricted,
                TestContext.Current.CancellationToken)));
        Assert.Equal(AccountState.Restricted, await StateAsync(restricted));
        Assert.Empty(_directory.Lifted);
    }

    /// <summary>
    /// PRIV-RIGHT-004 (D-166): lifting a restriction changes another person's account, so
    /// it is the <c>account:restrictionlift</c> step-up action, judged after the state: a
    /// session whose proof is not recent lifts nothing, and an account with no
    /// restriction is answered as one before any proof is asked.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_004_LiftingARestrictionAsksForStepUpAsync()
    {
        _directory.Stands(_member, AccountState.Restricted);

        var active = SubjectId.New(_randomness);

        _directory.Stands(active, AccountState.Active);

        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await LiftAsync(active, Stale)));
        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await LiftAsync(_member, Stale)));
        Assert.Equal(AccountState.Restricted, await StateAsync(_member));
        Assert.Empty(_directory.Lifted);
        Assert.Empty(_audit.Administered);
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// IDN-LIFE-003: a window an out-of-band erasure request began is cancelled on the
    /// subject's behalf and recorded against that request, and the account comes back
    /// as it stood.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AnOutOfBandDeletionIsCancelledAgainstItsRequestAsync()
    {
        var request = PrivacyRequestId.Of(Noon.AddDays(-1));

        _directory.Deleting(_member, DeletionOrigin.OutOfBandRequest, Noon.AddDays(-1));
        _directory.ErasedFor(_member, request);

        Accepted(await CancelAsync(_member));

        Assert.Equal(AccountState.Active, await StateAsync(_member));
        Assert.Equal(_administrator, Assert.Single(_events.Of<AccountDeletionCancelled>()).Actor);
        Assert.Equal(
            new RecordedChange(AuditActions.DeletionCancelled, _administrator, _member, Noon),
            Assert.Single(_audit.Administered));
        Assert.Equal(request, Assert.Single(_audit.Against));
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// IDN-LIFE-014: a window the subject began is cancelled on their behalf, which spends
    /// the link their notice carried and records no request.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_ASelfDeletionIsCancelledOnTheSubjectsBehalfAsync()
    {
        _directory.Deleting(_member, DeletionOrigin.Self, Noon.AddDays(-1));
        await _links.ReplaceAsync(
            LifecycleLink.Issued(
                _member,
                LifecycleLinkKind.DeletionCancellation,
                OpaqueToken.Draw(_randomness),
                Noon.AddDays(-1)),
            TestContext.Current.CancellationToken);

        Accepted(await CancelAsync(_member));

        Assert.Equal(AccountState.Active, await StateAsync(_member));
        Assert.Empty(_links.Links);
        Assert.Null(Assert.Single(_audit.Against));
    }

    /// <summary>
    /// IDN-LIFE-003: a takedown is refused as one, a window that has closed is refused as
    /// closed, an account in no window has nothing to cancel, and an unknown subject is
    /// not found; none changes anything.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_ATakedownOrAClosedWindowIsNotCancelledAsync()
    {
        var takenDown = SubjectId.New(_randomness);
        var closed = SubjectId.New(_randomness);

        _directory.Deleting(takenDown, DeletionOrigin.Takedown, Noon.AddDays(-1));
        _directory.Deleting(closed, DeletionOrigin.Self, Noon - Settings.AccountDeletionGrace.Default);

        Assert.Equal(ErrorCodes.TakedownActive, Refused(await CancelAsync(takenDown)));
        Assert.Equal(ErrorCodes.DeletionWindowElapsed, Refused(await CancelAsync(closed)));
        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await CancelAsync(_member)));
        Assert.Equal(ErrorCodes.AccountNotFound, Refused(await CancelAsync(SubjectId.New(_randomness))));
        Assert.Equal(AccountState.Deleting, await StateAsync(takenDown));
        Assert.Equal(AccountState.Deleting, await StateAsync(closed));
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-LIFE-003 (D-166): cancelling a deletion on the subject's behalf changes
    /// another person's account, so it is the <c>account:deletioncancel</c> step-up
    /// action, judged after every other refusal: a session whose proof is not recent
    /// cancels nothing, and a closed window is answered as closed before any proof is
    /// asked.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_ACancellationOnTheSubjectsBehalfAsksForStepUpAsync()
    {
        var closed = SubjectId.New(_randomness);

        _directory.Deleting(_member, DeletionOrigin.Self, Noon.AddDays(-1));
        _directory.Deleting(closed, DeletionOrigin.Self, Noon - Settings.AccountDeletionGrace.Default);

        Assert.Equal(ErrorCodes.DeletionWindowElapsed, Refused(await CancelAsync(closed, Stale)));
        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await CancelAsync(_member, Stale)));
        Assert.Equal(AccountState.Deleting, await StateAsync(_member));
        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Administered);
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// IDN-ATTR-003 AC3: the photo an account shows is read through the gate, so an
    /// administrator reads it, an unknown subject is not found, and a person without the
    /// permission is refused it.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_003_AC3_AnAccountsPhotoIsReadThroughTheGateAsync()
    {
        var organization = OrganizationId.New(_clock);
        byte[] image = [0xFF, 0xD8, 0xFF, 0xD9];

        _memberships.Place(_member, organization);
        _configuration.Set(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault with { Photos = true });
        _directory.Shows(_member, image);

        Assert.Equal(image, (await PhotoAsync(Acting, _member)).Match(read => read.ToArray(), _ => []));
        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await PhotoAsync(Acting, SubjectId.New(_randomness))));
        Assert.Equal(ErrorCodes.Denied, Refused(await PhotoAsync(AccessContext.Of(_member), _member)));
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-ATTR-002, IDN-ATTR-003 (D-166): a photo is read for an administrator only
    /// where the policy in force for the account shows photos, and an account that shows
    /// none answers alike, with <c>identity.photo.notfound</c>.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_002_APhotoThePolicyWithholdsIsNotReadAsync()
    {
        var organization = OrganizationId.New(_clock);
        var bare = SubjectId.New(_randomness);

        _directory.Stands(bare, AccountState.Active);
        _memberships.Place(_member, organization);
        _memberships.Place(bare, organization);
        _configuration.Set(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault with { Photos = true });
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            PolicyOverride.None with { Photos = false });
        _directory.Shows(_member, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });

        Assert.Equal(ErrorCodes.PhotoNotFound, Refused(await PhotoAsync(Acting, _member)));

        _configuration.Set(Settings.OrganizationPolicy, organization.ToString(), PolicyOverride.None);

        Assert.Equal(ErrorCodes.PhotoNotFound, Refused(await PhotoAsync(Acting, bare)));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a suspension refused under the account's lock, for a deletion
    /// begun since it was first judged, rolls its unit of work back and commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ASuspensionRefusedUnderTheLockIsRolledBackAsync()
    {
        _directory.Holding = held => _directory.Deleting(held, DeletionOrigin.Self, Noon);

        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await SuspendAsync(_member)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a reactivation refused under the account's lock, for an
    /// account that came back since it was first judged, rolls its unit of work back and
    /// commits nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AReactivationRefusedUnderTheLockIsRolledBackAsync()
    {
        _directory.Suspended(_member, SuspensionOrigin.Administrator);
        _directory.Holding = held => _directory.Stands(held, AccountState.Active);

        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await ReactivateAsync(_member)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a lift refused under the account's lock, for a restriction
    /// that ended since it was first judged, rolls its unit of work back and commits
    /// nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ALiftRefusedUnderTheLockIsRolledBackAsync()
    {
        _directory.Stands(_member, AccountState.Restricted);
        _directory.Holding = held => _directory.Stands(held, AccountState.Active);

        Assert.Equal(ErrorCodes.AccountStateConflict, Refused(await LiftAsync(_member)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a cancellation refused under the account's lock, for a
    /// takedown begun since it was first judged, rolls its unit of work back and commits
    /// nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACancellationRefusedUnderTheLockIsRolledBackAsync()
    {
        _directory.Deleting(_member, DeletionOrigin.Self, Noon.AddDays(-1));
        _directory.Holding = held => _directory.Deleting(held, DeletionOrigin.Takedown, Noon);

        Assert.Equal(ErrorCodes.TakedownActive, Refused(await CancelAsync(_member)));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    private static DateTimeOffset Stale =>
        Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1);

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private static ErrorCode Refused<T>(Result<T> outcome) =>
        outcome.Match<ErrorCode>(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private static Error Refusal(Result outcome) =>
        outcome.Match(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error);

    private async Task<Result<ReadOnlyMemory<byte>>> PhotoAsync(AccessContext context, SubjectId subject) =>
        await Administration.ReadPhotoAsync(context, subject, TestContext.Current.CancellationToken);

    private async Task<Result> SuspendAsync(SubjectId subject) =>
        await Administration.SuspendAsync(
            Acting,
            Opened(_administrator, Noon),
            subject,
            TestContext.Current.CancellationToken);

    private async Task<Result> ReactivateAsync(SubjectId subject) =>
        await Administration.ReactivateAsync(
            Acting,
            Opened(_administrator, Noon),
            subject,
            TestContext.Current.CancellationToken);

    private async Task<Result> CancelAsync(SubjectId subject, DateTimeOffset? proved = null) =>
        await Administration.CancelDeletionAsync(
            Acting,
            Opened(_administrator, proved ?? Noon),
            subject,
            TestContext.Current.CancellationToken);

    private async Task<Result> LiftAsync(SubjectId subject, DateTimeOffset? proved = null) =>
        await Administration.LiftRestrictionAsync(
            Acting,
            Opened(_administrator, proved ?? Noon),
            subject,
            TestContext.Current.CancellationToken);

    private async Task<AccountState?> StateAsync(SubjectId subject) =>
        await _directory.StateAsync(subject, TestContext.Current.CancellationToken);

    private async Task<IReadOnlyList<Session>> LiveAsync(SubjectId subject) =>
        await _sessions.LiveOfAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken);

    private SessionId Opened(SubjectId subject, DateTimeOffset at)
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
}
