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
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

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

        await Service.ReuseAsync(subject, session, Protocol, Cancellation);

        Assert.NotNull(_sessions.All.Single(live => live.Id == session).EndedAt);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MintAsync(session, Cancellation)));
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

        await Service.ReuseAsync(subject, session, Protocol, Cancellation);

        (SubjectId Subject, string ClientId, SessionId Session, DateTimeOffset At) recorded =
            _audit.Reuses.Single();

        Assert.Equal((subject, Protocol, session), (recorded.Subject, recorded.ClientId, recorded.Session));
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

        OidcClaims held = Value(await Service.ClaimsAsync(subject, "openid", Cancellation))!;
        OidcClaims withEmail = Value(await Service.ClaimsAsync(subject, "openid email", Cancellation))!;

        Assert.Null(held.Email);
        Assert.Equal(Address, withEmail.Email);
        Assert.True(withEmail.EmailVerified);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);

    private SigningKeys Keys => new(_keys, _configuration, _work, _clock);

    private OidcService Service => new(
        Keys,
        _sessions,
        _identifiers,
        _accounts,
        _audit,
        _configuration,
        _work,
        _clock);

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
                satisfiesEveryGate: true),
            [1],
            [2],
            Cancellation);

        return id;
    }
}
