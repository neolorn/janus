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
/// What an account does to its own standing: deactivation and the link that
/// reverses it, and the deletion window and the link that ends it (IDN-LIFE-013,
/// IDN-LIFE-014, IDN-ACCT-007, AUTH-SESS-010).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountLifecycleTests : IAsyncDisposable
{
    private const string Source = "198.51.100.7";
    private const string Primary = "primary@example.test";

    private static readonly string[] English = ["en"];

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(Source, new DeviceDescription("Firefox", "Fedora"));

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of([]);

    private readonly AccountDirectoryInMemory _directory = new(Declared);
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly LifecycleLinkStoreInMemory _links = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AccountAuditInMemory _audit = new();
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
    /// An active account holding one verified email and a password, which is what
    /// the shortest registration leaves behind.
    /// </summary>
    public AccountLifecycleTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, English);
        _person = SubjectId.New(_randomness);
        _directory.Stands(_person, AccountState.Active);
        _passwords.Hold(_person, Noon);
        _ = _identifiers.Verified(_person, IdentifierKind.Email, Primary);

    }

    private AccountLifecycle Lifecycle =>
        new(
            _directory,
            _identifiers,
            _links,
            _sessions,
            _notifications,
            _audit,
            new StepUpGuard(
                _sessions,
                _authenticators,
                _passwords,
                new PolicyResolution(_memberships, _configuration, _raises),
                _clock),
            _events,
            _configuration,
            _work,
            _clock,
            _randomness);

    private AccessContext Acting => AccessContext.Of(_person);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-LIFE-013 AC1: the link the notice carried stands the account back up
    /// exactly as it was, with everything it held still held.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AC1_TheLinkRestoresTheAccountAndWhatItHeldAsync()
    {
        Accepted(await Lifecycle.DeactivateAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Suspended, await StateAsync());
        Assert.Equal(SuspensionOrigin.Self, await SuspendedByAsync());

        Accepted(await Lifecycle.ReactivateAsync(Link(), TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Active, await StateAsync());
        Assert.Null(await SuspendedByAsync());

        // Nothing was removed to suspend the account, so the identifier it signs in
        // with and the password it signs in by are where they were.
        Assert.Equal(
            Primary,
            Assert.Single((await _identifiers.HeldAsync(
                _person,
                TestContext.Current.CancellationToken)).All).Canonical);

        Assert.NotNull(await _passwords.FindAsync(_person, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-013: the link is spent when it is used, so the same one presented
    /// twice stands nothing up the second time.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_ASpentLinkStandsNothingUpAsync()
    {
        Accepted(await Lifecycle.DeactivateAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        string link = Link();

        Accepted(await Lifecycle.ReactivateAsync(link, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.ReactivationTokenInvalid,
            Refused(await Lifecycle.ReactivateAsync(link, TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.ReactivationTokenInvalid,
            Refused(await Lifecycle.ReactivateAsync(
                "nothing-was-issued",
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-LIFE-013: an administrator's suspension is an administrator's to
    /// reverse, whatever link the account is still holding.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AdministrativeSuspensionIsNotReversedByALinkAsync()
    {
        Accepted(await Lifecycle.DeactivateAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        _directory.Suspended(_person, SuspensionOrigin.Administrator);

        Assert.Equal(
            ErrorCodes.AccountAdministrativelySuspended,
            Refused(await Lifecycle.ReactivateAsync(Link(), TestContext.Current.CancellationToken)));

        Assert.Equal(AccountState.Suspended, await StateAsync());
    }

    /// <summary>
    /// IDN-LIFE-013: deactivation is a step-up action, and a session that has not
    /// stepped up takes nothing down.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_DeactivationWithoutStepUpTakesNothingDownAsync()
    {
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Lifecycle.DeactivateAsync(
                Acting,
                Stale(),
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(AccountState.Active, await StateAsync());
        Assert.Empty(_notifications.Mail);
    }

    /// <summary>
    /// AUTH-SESS-010 AC1: the transition out of active ends the account's sessions
    /// in the operation that makes it, and not only the one it was asked on.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_AC1_DeactivationEndsTheAccountsSessionsAsync()
    {
        SessionId session = Stepped();
        _ = Opened(Noon);

        Assert.Equal(2, (await LiveAsync()).Count);

        Accepted(await Lifecycle.DeactivateAsync(
            Acting,
            session,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(await LiveAsync());
    }

    /// <summary>
    /// IDN-LIFE-014: the window the deployment configures is what the account is
    /// told, and nothing of the account's is live through it.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_TheDeletionWindowIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.AccountDeletionGrace, TimeSpan.FromDays(14));

        DateTimeOffset erasesAt = Value(await Lifecycle.DeleteAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Noon + TimeSpan.FromDays(14), erasesAt);
        Assert.Equal(AccountState.Deleting, await StateAsync());
        Assert.Empty(await LiveAsync());

        HeldDeletion held = (await _directory.DeletingAsync(
            _person,
            TestContext.Current.CancellationToken))!;

        Assert.Equal(DeletionOrigin.Self, held.By);
        Assert.Equal(Noon, held.Since);
        Assert.Equal(MessageKind.DeletionNotice, _notifications.Mail.Single().Message);
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: the link cancels the deletion anywhere inside the window
    /// and the account is active again.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC4_TheLinkCancelsThroughoutTheWindowAsync()
    {
        _ = Value(await Lifecycle.DeleteAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        _clock.Advance(Settings.AccountDeletionGrace.Default - TimeSpan.FromMinutes(1));

        Accepted(await Lifecycle.CancelDeletionAsync(Link(), TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Active, await StateAsync());

        Assert.Null(await _directory.DeletingAsync(_person, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: the window is cancellable throughout and no longer, and a
    /// token that answers to nothing is told the same thing.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC4_AWindowThatHasRunOutCancelsNothingAsync()
    {
        _ = Value(await Lifecycle.DeleteAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        string link = Link();

        _clock.Advance(Settings.AccountDeletionGrace.Default);

        Assert.Equal(
            ErrorCodes.DeletionWindowElapsed,
            Refused(await Lifecycle.CancelDeletionAsync(
                link,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.DeletionWindowElapsed,
            Refused(await Lifecycle.CancelDeletionAsync(
                "nothing-was-issued",
                TestContext.Current.CancellationToken)));

        Assert.Equal(AccountState.Deleting, await StateAsync());
    }

    /// <summary>
    /// IDN-LIFE-003: a takedown is not the subject's to undo, and says so rather
    /// than answering as a window that has run out.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_ATakedownIsNotCancelledByTheSubjectsLinkAsync()
    {
        _ = Value(await Lifecycle.DeleteAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        _directory.Deleting(_person, DeletionOrigin.Takedown, Noon);

        Assert.Equal(
            ErrorCodes.TakedownActive,
            Refused(await Lifecycle.CancelDeletionAsync(
                Link(),
                TestContext.Current.CancellationToken)));

        Assert.Equal(AccountState.Deleting, await StateAsync());
    }

    /// <summary>
    /// IDN-LIFE-014: deletion is a step-up action, and a session that has not
    /// stepped up starts no window.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_DeletionWithoutStepUpStartsNoWindowAsync()
    {
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Lifecycle.DeleteAsync(
                Acting,
                Stale(),
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(AccountState.Active, await StateAsync());
        Assert.Empty(_notifications.Mail);
    }

    /// <summary>
    /// IDN-LIFE-013, IDN-LIFE-014: what the account did to itself is audited and
    /// announced, so a host acting on the standing hears of both.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_WhatTheAccountDidToItselfIsAuditedAndAnnouncedAsync()
    {
        Accepted(await Lifecycle.DeactivateAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        Accepted(await Lifecycle.ReactivateAsync(Link(), TestContext.Current.CancellationToken));

        _ = Value(await Lifecycle.DeleteAsync(
            Acting,
            Stepped(),
            Source,
            TestContext.Current.CancellationToken));

        Accepted(await Lifecycle.CancelDeletionAsync(Link(), TestContext.Current.CancellationToken));

        Assert.Equal<IEnumerable<string>>(
            [
                "identity.account.deactivated",
                "identity.account.reactivated",
                "identity.deletion.requested",
                "identity.deletion.cancelled",
            ],
            [.. _audit.Recorded.Select(change => change.Action.ToString())]);

        Assert.Equal(SuspensionOrigin.Self, Assert.Single(_events.Of<AccountSuspended>()).By);
        _ = Assert.Single(_events.Of<AccountReactivated>());
        Assert.Equal(DeletionOrigin.Self, Assert.Single(_events.Of<AccountDeletionRequested>()).By);
        _ = Assert.Single(_events.Of<AccountDeletionCancelled>());
    }

    /// <summary>
    /// CONV-DESIGN-005 AC1 and D-022: an event the port would not take fails the
    /// operation that made it, so nothing is committed that no consumer was told of.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_005_AC1_AnEventThatIsNotTakenFailsTheOperationAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Lifecycle.DeactivateAsync(
                Acting,
                Stepped(),
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(0, _work.Committed);
        Assert.Empty(_events.Published);
        Assert.Empty(_audit.Recorded);
    }

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused(Result outcome) =>
        outcome.Match<ErrorCode>(
            () => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private static TValue Value<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    // The token the notice carried, read off the body the template put it in.
    private string Link() => _notifications.Mail[^1].Values["token"];

    private async Task<AccountState?> StateAsync() =>
        await _directory.StateAsync(_person, TestContext.Current.CancellationToken);

    private async Task<SuspensionOrigin?> SuspendedByAsync() =>
        await _directory.SuspendedByAsync(_person, TestContext.Current.CancellationToken);

    private async Task<IReadOnlyList<Session>> LiveAsync() =>
        await _sessions.LiveOfAsync(_person, _clock.GetUtcNow(), TestContext.Current.CancellationToken);

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
}
