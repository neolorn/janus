using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
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
                _clock),
            _directory,
            _sessions,
            _links,
            _configuration,
            _events,
            _audit,
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
    /// nothing to reverse.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_OnlyAnAdministratorsSuspensionIsReactivatedAsync()
    {
        var deactivated = SubjectId.New(_randomness);

        _directory.Suspended(deactivated, SuspensionOrigin.Self);

        Assert.Equal(ErrorCodes.Denied, Refused(await ReactivateAsync(deactivated)));
        Assert.Equal(ErrorCodes.Denied, Refused(await ReactivateAsync(_member)));
        Assert.Equal(AccountState.Suspended, await StateAsync(deactivated));
        Assert.Equal(
            SuspensionOrigin.Self,
            await _directory.SuspendedByAsync(deactivated, TestContext.Current.CancellationToken));
        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Administered);
    }

    /// <summary>
    /// IDN-LIFE-013: an account in its deletion window or erased is past suspending, and
    /// an unknown subject is named as the part of the request that is wrong.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AnAccountBeingDeletedOrUnknownIsNotSuspendedAsync()
    {
        _directory.Deleting(_member, DeletionOrigin.Self, Noon);

        Assert.Equal(ErrorCodes.Denied, Refused(await SuspendAsync(_member)));
        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await SuspendAsync(SubjectId.New(_randomness))));
        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await ReactivateAsync(SubjectId.New(_randomness))));
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
    /// the subscribers, and is recorded as the administrator's act, with no step-up asked.
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

        Assert.Equal(ErrorCodes.Denied, Refused(await LiftAsync(held)));
        Assert.Equal(ErrorCodes.Denied, Refused(await LiftAsync(_member)));
        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await LiftAsync(SubjectId.New(_randomness))));
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Administration.LiftRestrictionAsync(
                AccessContext.Of(stranger),
                restricted,
                TestContext.Current.CancellationToken)));
        Assert.Equal(AccountState.Restricted, await StateAsync(restricted));
        Assert.Empty(_directory.Lifted);
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
    /// named; none changes anything.
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
        Assert.Equal(ErrorCodes.Denied, Refused(await CancelAsync(_member)));
        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await CancelAsync(SubjectId.New(_randomness))));
        Assert.Equal(AccountState.Deleting, await StateAsync(takenDown));
        Assert.Equal(AccountState.Deleting, await StateAsync(closed));
        Assert.Empty(_audit.Administered);
    }

    private static DateTimeOffset Stale =>
        Noon - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1);

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

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

    private async Task<Result> CancelAsync(SubjectId subject) =>
        await Administration.CancelDeletionAsync(Acting, subject, TestContext.Current.CancellationToken);

    private async Task<Result> LiftAsync(SubjectId subject) =>
        await Administration.LiftRestrictionAsync(Acting, subject, TestContext.Current.CancellationToken);

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
}
