using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// What the library owns of the provider: the session record every token is minted
/// from, and what a token presented twice costs the whole of it (AUTH-OIDC-001 to
/// AUTH-OIDC-004, AUTH-SESS-012, BFF-SESS-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class OidcServiceTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Address = "person@example.test";
    private const string Protocol = "mail-server";
    private const string Grant = "0199a1b2-0c3d-7e4f-8a5b-6c7d8e9f0a1b";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Inactivity = TimeSpan.FromHours(8);

    private static readonly TimeSpan Absolute = TimeSpan.FromDays(7);

    private static readonly SessionOrigin Somewhere = new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"))
    {
        Location = new SessionLocation("Alexandria", "EG"),
    };

    private readonly SigningKeyStoreInMemory _keys = new();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly OidcAuditInMemory _audit = new();
    private readonly EventsInMemory _events = new();
    private readonly OidcGrantStoreInMemory _grants;
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly SigningProcess _signing;

    /// <summary>
    /// Starts the credential source the service reads the key set from, and the store
    /// it revokes a grant in.
    /// </summary>
    public OidcServiceTests()
    {
        _signing = new SigningProcess(_keys, _configuration, _clock);
        _grants = new OidcGrantStoreInMemory(_work);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _signing.DisposeAsync();
        await _work.DisposeAsync();
    }

    /// <summary>
    /// AUTH-SESS-012 AC2, BFF-SESS-006 AC1: a record that answers mints for the
    /// account it names, with nobody asked anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC2_ALiveRecordMintsForTheAccountItNamesAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        Assert.Equal(subject, Value(await Service.MintAsync(session, Cancellation))!.Subject);
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: a record the deployment does not hold mints nothing, so a
    /// request carrying one that has already gone is refused rather than served.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_ARecordTheDeploymentDoesNotHoldMintsNothingAsync() =>
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(new SessionId(Guid.NewGuid()), Cancellation)));

    /// <summary>
    /// AUTH-OIDC-004 AC1, BFF-SESS-006 AC5: no token is minted from a record that has
    /// been revoked, so nothing issued before the revocation stands after it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC1_NoTokenIsMintedFromARevokedRecordAsync()
    {
        SessionId session = await SignedInAsync();

        await _sessions.EndSpineAsync(session, _clock.GetUtcNow(), Cancellation);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(session, Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC1: no token is minted from a record that has been left alone
    /// past its inactivity window.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC1_NoTokenIsMintedFromAnIdleRecordAsync()
    {
        SessionId session = await SignedInAsync();

        _clock.Advance(Inactivity);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(session, Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC1: no token is minted from a record that has reached the
    /// ceiling no activity moves.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC1_NoTokenIsMintedFromAnExpiredRecordAsync()
    {
        SessionId session = await SignedInAsync(Absolute, TimeSpan.FromHours(1));

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(session, Cancellation)));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC2: what is minted carries the configured access-token lifetime.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC2_TheAccessTokenLifetimeIsTheConfiguredOneAsync()
    {
        SessionId session = await SignedInAsync();

        Assert.Equal(
            TimeSpan.FromMinutes(10),
            Value(await Service.MintAsync(session, Cancellation))!.AccessTokenLifetime);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC3: what is minted never outlives the record, so the window is
    /// what remains of whichever of its two expiries comes first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC3_WhatIsMintedNeverOutlivesTheRecordAsync()
    {
        SessionId session = await SignedInAsync();

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(
            Inactivity - TimeSpan.FromHours(1),
            Value(await Service.MintAsync(session, Cancellation))!.Remaining);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC1: a token presented a second time ends the record and
    /// everything derived from it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_AReuseEndsEverythingDerivedFromTheRecordAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation);

        Assert.NotNull(_sessions.All.Single(live => live.Id == session).EndedAt);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(session, Cancellation)));
    }

    /// <summary>
    /// LIB-API-001 (D-219): each session a reuse ends is announced with the reason
    /// <c>security</c>, naming no actor.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_001_EachSessionAReuseEndsIsAnnouncedAsSecurityAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation);

        SessionEnded ended = Assert.Single(_events.Of<SessionEnded>());

        Assert.Equal(
            (session, SessionEndReason.Security, subject, (SubjectId?)null),
            (ended.Session, ended.Reason, ended.Subject, ended.Actor));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC2: the revocation is recorded, with the client that presented
    /// the token and the record everything stood on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC2_TheRevocationIsAuditedAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation);

        (SubjectId? Subject, string ClientId, SessionId? Session, DateTimeOffset At) recorded =
            _audit.Reuses.Single();

        Assert.Equal<(SubjectId?, string, SessionId?)>(
            (subject, Protocol, session),
            (recorded.Subject, recorded.ClientId, recorded.Session));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a reuse, whose revocation stands
    /// whatever follows, called inside a unit of work another opened is a fault of the
    /// calling code: it ends the level it joined before it writes anything, so nothing
    /// of the outer unit of work commits and the record stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AReuseInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        _work.Reset();
        _ = await _work.BeginAsync(Cancellation);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _work.CommitAsync(Cancellation));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Null(_sessions.All.Single(live => live.Id == session).EndedAt);
        Assert.Empty(_grants.Revoked);
        Assert.Empty(_audit.Reuses);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-207): a reuse revokes the whole family, the spine's sessions,
    /// the provider's grant with its tokens, and writes its audit record, all inside the
    /// one outermost unit of work it begins, committed once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndRecordsItInOneCommitAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        _work.Reset();

        await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation);

        Assert.Equal([(Grant, true)], _grants.Revoked);
        Assert.NotNull(_sessions.All.Single(live => live.Id == session).EndedAt);
        _ = Assert.Single(_audit.Reuses);
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-207): where the commit fails, the revocation of the grant and
    /// its tokens does not stand, and the reuse is a fault, so the next presentation of
    /// the redeemed copy revokes again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AFailedCommitLeavesTheGrantStandingAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId subject = _sessions.All.Single(live => live.Id == session).Subject;

        _work.Reset();
        _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Service.ReuseAsync(subject, session, Protocol, Grant, Cancellation));

        Assert.Empty(_grants.Revoked);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-208): a redeemed token naming no session and no subject
    /// revokes its grant and every token of it, ends no session, and is recorded under
    /// the account its grant records and no session, in one commit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync()
    {
        SessionId standing = await SignedInAsync();
        SubjectId granted = _sessions.All.Single(live => live.Id == standing).Subject;
        _grants.Hold(Grant, granted);

        _work.Reset();

        await Service.ReuseAsync(subject: null, session: null, Protocol, Grant, Cancellation);

        Assert.Equal([(Grant, true)], _grants.Revoked);
        Assert.Null(_sessions.All.Single(live => live.Id == standing).EndedAt);
        Assert.Equal<(SubjectId?, string, SessionId?)>(
            (granted, Protocol, null),
            Recorded());
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-208): a redeemed token issued under no grant and naming no
    /// session and no subject revokes no grant and ends no session, and is recorded
    /// naming no account and no session, in one commit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderNoAccountAsync()
    {
        SessionId standing = await SignedInAsync();

        _work.Reset();

        await Service.ReuseAsync(subject: null, session: null, Protocol, grant: null, Cancellation);

        Assert.Empty(_grants.Revoked);
        Assert.Null(_sessions.All.Single(live => live.Id == standing).EndedAt);
        Assert.Equal<(SubjectId?, string, SessionId?)>((null, Protocol, null), Recorded());
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.Equal(0, _work.RolledBack);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC2 (D-208): a redeemed token naming a session but no subject,
    /// issued under no grant, ends the session's spine and is recorded under the account
    /// of that session, naming it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC2_AReuseNamingOnlyASessionIsRecordedUnderTheSessionsAccountAsync()
    {
        SessionId session = await SignedInAsync();
        SubjectId holder = _sessions.All.Single(live => live.Id == session).Subject;

        await Service.ReuseAsync(subject: null, session, Protocol, grant: null, Cancellation);

        Assert.NotNull(_sessions.All.Single(live => live.Id == session).EndedAt);
        Assert.Empty(_grants.Revoked);
        Assert.Equal<(SubjectId?, string, SessionId?)>((holder, Protocol, session), Recorded());
    }

    /// <summary>
    /// AUTH-OIDC-003 AC2 (D-208): the account the token names is recorded before the one
    /// its grant records, and the grant's before the session's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC2_TheTokensAccountIsRecordedBeforeTheGrantsAndTheGrantsBeforeTheSessionsAsync()
    {
        SessionId session = await SignedInAsync();
        var granted = new SubjectId(Guid.NewGuid());
        var named = new SubjectId(Guid.NewGuid());
        _grants.Hold(Grant, granted);

        await Service.ReuseAsync(named, session, Protocol, Grant, Cancellation);
        await Service.ReuseAsync(subject: null, session, Protocol, Grant, Cancellation);

        Assert.Equal<SubjectId?[]>(
            [named, granted],
            [.. _audit.Reuses.Select(reuse => reuse.Subject)]);
    }

    /// <summary>
    /// Chapter 09 section 9 (D-153): each scope names what it gives, and nothing
    /// outside those lists leaves the library.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_TheClaimsAnsweredAreTheOnesTheScopeNamesAsync()
    {
        SubjectId subject = await AccountAsync();

        OidcClaims held = Value(await Service.ClaimsAsync(AccessContext.Of(subject), "openid", Cancellation))!;
        OidcClaims withEmail = Value(await Service.ClaimsAsync(AccessContext.Of(subject), "openid email", Cancellation))!;

        Assert.Null(held.Email);
        Assert.Equal(Address, withEmail.Email);
        Assert.True(withEmail.EmailVerified);
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166): a restricted account signs on to the mail server as an
    /// active one does, so its claims are answered, while a suspended account's are not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountsClaimsAreAnsweredAsync()
    {
        SubjectId restricted = await AccountAsync();
        SubjectId suspended = await AccountAsync();

        _accounts.Stands(restricted, AccountState.Restricted);
        _accounts.Stands(suspended, AccountState.Suspended);

        Assert.Equal(Address, Value(await Service.ClaimsAsync(AccessContext.Of(restricted), "openid email", Cancellation))!.Email);
        Assert.Equal(ErrorCodes.Denied, Refused(await Service.ClaimsAsync(AccessContext.Of(suspended), "openid email", Cancellation)));
    }

    /// <summary>
    /// LIB-API-005 (D-166, 160): a caller in process reads the claims of the identity its
    /// context carries and no other account's, and a context naming no person reads
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_005_ClaimsAnswerOnlyTheEffectiveSubjectAsync()
    {
        SubjectId subject = await AccountAsync();
        SubjectId other = await AccountAsync();

        OidcClaims own = Value(await Service.ClaimsAsync(AccessContext.Of(subject), "openid", Cancellation))!;
        OidcClaims others = Value(await Service.ClaimsAsync(AccessContext.Of(other), "openid", Cancellation))!;
        ErrorCode principal = Refused(await Service.ClaimsAsync(
            AccessContext.Of(SystemPrincipal.ForDeployment("claims-reader", "LIB-API-005", SystemOperation.Configuration)),
            "openid",
            Cancellation));

        Assert.Equal(subject, own.Subject);
        Assert.Equal(other, others.Subject);
        Assert.Equal(ErrorCodes.Denied, principal);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);

    private OidcService Service => new(
        _signing.Source,
        _sessions,
        _identifiers,
        _accounts,
        _grants,
        _audit,
        _events,
        _configuration,
        _work,
        _clock);

    // The one reuse recorded: whose account, which client and which session it names.
    private (SubjectId? Subject, string ClientId, SessionId? Session) Recorded()
    {
        (SubjectId? Subject, string ClientId, SessionId? Session, DateTimeOffset At) recorded =
            _audit.Reuses.Single();

        return (recorded.Subject, recorded.ClientId, recorded.Session);
    }

    private async ValueTask<SubjectId> AccountAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        await _identifiers.PromoteAsync(subject, email, Cancellation);

        return subject;
    }

    private ValueTask<SessionId> SignedInAsync() => SignedInAsync(Inactivity, Absolute);

    private async ValueTask<SessionId> SignedInAsync(TimeSpan inactivity, TimeSpan absolute)
    {
        SubjectId subject = await AccountAsync();
        var id = new SessionId(Guid.NewGuid());

        await _sessions.AddAsync(
            Session.Begin(
                id,
                subject,
                new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                Somewhere,
                _clock.GetUtcNow(),
                inactivity,
                absolute,
                breakGlassReason: "The operator cannot be reached."),
            [1],
            [2],
            Cancellation);

        return id;
    }
}
