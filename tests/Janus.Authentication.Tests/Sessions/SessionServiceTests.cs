using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// The session spine: what a sign-in records, how long it lives, what refreshes it,
/// and what ends it (AUTH-SESS-001 to AUTH-SESS-013).
/// </summary>
[Trait("kind", "unit")]
public sealed class SessionServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere =
        new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"));

    private readonly SessionStoreInMemory _sessions = new();
    private readonly SessionAuditInMemory _audit = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly LocationResolverInMemory _locations = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private SessionService Service =>
        new(
            _sessions,
            _audit,
            new PolicyResolution(_memberships, _configuration, _raises),
            _configuration,
            _gate,
            _locations,
            _work,
            _clock,
            _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-SESS-002 AC1: no session field names a factor.
    /// </summary>
    [Fact]
    public void AUTH_SESS_002_AC1_NoSessionFieldNamesAFactor()
    {
        IEnumerable<Type> held = typeof(Session)
            .GetProperties()
            .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);

        Assert.DoesNotContain(typeof(Factor), held);
        Assert.DoesNotContain(
            typeof(Session).GetProperties(),
            property => property.Name.Contains("Factor", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC1: no session field names an organization, so an evaluation
    /// has nowhere to read one from but the resource.
    /// </summary>
    [Fact]
    public void AUTHZ_SCOPE_001_AC1_NoSessionFieldNamesAnOrganization()
    {
        IEnumerable<Type> held = typeof(Session)
            .GetProperties()
            .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);

        Assert.DoesNotContain(typeof(OrganizationId), held);
        Assert.DoesNotContain(
            typeof(Session).GetProperties(),
            property => property.Name.Contains("Organization", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTH-SESS-002 AC2: the audit record for the authentication does name the
    /// factor.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_002_AC2_TheAuditRecordNamesTheFactorAsync()
    {
        SubjectId subject = Subject();

        await BegunAsync(subject, [Factor.Password]);

        (SessionId _, SubjectId who, IReadOnlyCollection<Factor> presented) =
            Assert.Single(_audit.Records);
        Assert.Equal(subject, who);
        Assert.Equal([Factor.Password], presented);
    }

    /// <summary>
    /// AUTH-SESS-003 AC3: the cookie value is opaque, yielding no information when
    /// decoded and nothing the record can be found by.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_003_AC3_TheCookieValueYieldsNothingWhenDecodedAsync()
    {
        SubjectId subject = Subject();
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        Assert.DoesNotContain(
            issued.Id.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            issued.Secret.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            subject.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            issued.Secret.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(32, System.Buffers.Text.Base64Url.DecodeFromChars(issued.Secret.Value).Length);
    }

    /// <summary>
    /// AUTH-SESS-004 AC3, AUTH-SESS-001 AC1: revoking the record terminates what
    /// derives from it.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_004_AC3_RevokingTheRecordTerminatesWhatDerivesFromItAsync()
    {
        SubjectId subject = Subject();
        IssuedSession record = await BegunAsync(subject, [Factor.Password]);
        IssuedSession app = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));

        await Service.EndAsync(
            AccessContext.Of(subject),
            record.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.SessionExpired,
            await RefusalAsync(app.Secret));
    }

    /// <summary>
    /// AUTH-SESS-012 AC7: the derived session stands on the record, inheriting what
    /// it proved and ending no later than it does.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_012_AC7_TheDerivedSessionStandsOnTheRecordAsync()
    {
        SubjectId subject = Subject();
        IssuedSession record = await BegunAsync(subject, [Factor.Passkey]);
        IssuedSession app = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));

        Session derived = _sessions.Behind(app.Secret)!;
        Session spine = _sessions.Behind(record.Secret)!;

        Assert.Equal(spine.Id, derived.Spine);
        Assert.Equal(spine.Attained, derived.Attained);
        Assert.Equal(spine.PhishingResistant, derived.PhishingResistant);
        Assert.Equal(spine.AbsoluteExpiry, derived.AbsoluteExpiry);
    }

    /// <summary>
    /// AUTH-SESS-005 AC1: a customer session used at least once every ninety days
    /// expires only at three hundred and sixty-five days from sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC1_ACustomerSessionInUseExpiresAtTheAbsoluteLimitAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Password]);

        for (int month = 0; month < 12; month++)
        {
            _clock.Advance(TimeSpan.FromDays(30));
            Assert.Null(await RefusalAsync(issued.Secret));
        }

        _clock.Advance(TimeSpan.FromDays(6));

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
    }

    /// <summary>
    /// AUTH-SESS-005 AC2: a customer session unused for ninety days expires, and one
    /// signed in with a passkey has the same lifetime as one signed in with a
    /// password.
    /// </summary>
    /// <param name="factor">What signed in.</param>
    [Theory]
    [InlineData(Factor.Password)]
    [InlineData(Factor.Passkey)]
    public async Task AUTH_SESS_005_AC2_ACustomerSessionHasOneLifetimeWhateverSignedItInAsync(
        Factor factor)
    {
        IssuedSession issued = await BegunAsync(Subject(), [factor]);

        _clock.Advance(TimeSpan.FromDays(89));
        Assert.Null(await RefusalAsync(issued.Secret));

        _clock.Advance(TimeSpan.FromDays(90));

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
    }

    /// <summary>
    /// AUTH-SESS-005a AC1: a passkey-only session records AAL2, and its lifetime
    /// follows the principal's policy and not the table.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005a_AC1_APasskeySessionRecordsAal2AndKeepsItsPolicysLifetimeAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Passkey]);
        Session session = _sessions.Behind(issued.Secret)!;

        Assert.Equal(AssuranceLevel.Aal2, session.Attained);
        Assert.True(session.PhishingResistant);
        Assert.Equal(Noon + TimeSpan.FromDays(365), session.AbsoluteExpiry);
        Assert.Equal(Noon + TimeSpan.FromDays(90), session.IdleExpiry);
    }

    /// <summary>
    /// AUTH-SESS-005 AC3: an administrative-organization session expires after an
    /// hour idle or a day absolute, whichever comes first.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC3_AStaffSessionExpiresAfterAnHourIdleOrADayAsync()
    {
        SubjectId subject = Staff();
        IssuedSession issued = await BegunAsync(subject, [Factor.Passkey]);
        Session session = _sessions.Behind(issued.Secret)!;

        Assert.Equal(Noon + TimeSpan.FromHours(1), session.IdleExpiry);
        Assert.Equal(Noon + TimeSpan.FromHours(24), session.AbsoluteExpiry);

        for (int hour = 0; hour < 23; hour++)
        {
            _clock.Advance(TimeSpan.FromMinutes(50));
            Assert.Null(await RefusalAsync(issued.Secret));
        }

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
    }

    /// <summary>
    /// AUTH-SESS-005 AC3a: a customer session expired for inactivity asks for a full
    /// authentication; the single-factor restore is never offered under the system
    /// policy.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC3a_ACustomerIdleExpiryAsksForAFullAuthenticationAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Password]);

        _clock.Advance(TimeSpan.FromDays(91));

        Assert.Equal("full", await AskedAsync(issued.Secret));
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refusal(await Service.RestoreAsync(
                issued.Secret,
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-005 AC4: after an idle expiry inside the absolute window, one
    /// eligible factor restores the same session; after the window, it does not.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_OneFactorRestoresTheSameSessionInsideTheWindowAsync()
    {
        SubjectId subject = Staff();
        IssuedSession issued = await BegunAsync(subject, [Factor.Passkey]);

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal("single-factor", await AskedAsync(issued.Secret));

        IssuedSession restored = Value(await Service.RestoreAsync(
            issued.Secret,
            [Factor.Passkey],
            Somewhere,
            TestContext.Current.CancellationToken));

        Assert.Equal(issued.Id, restored.Id);
        Assert.Null(await RefusalAsync(restored.Secret));

        _clock.Advance(TimeSpan.FromHours(23));

        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refusal(await Service.RestoreAsync(
                restored.Secret,
                [Factor.Passkey],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-005 AC2a: after an idle expiry a second factor alone does not
    /// restore the session; a factor bound to the session secret does.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC2a_ASecondFactorAloneRestoresNothingAsync()
    {
        SubjectId subject = Staff();
        IssuedSession issued = await BegunAsync(subject, [Factor.Passkey]);

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(
            ErrorCodes.FactorRequired,
            Refusal(await Service.RestoreAsync(
                issued.Secret,
                [Factor.Totp],
                Somewhere,
                TestContext.Current.CancellationToken)));
        Assert.NotNull(Value(await Service.RestoreAsync(
            issued.Secret,
            [Factor.Passkey],
            Somewhere,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-005b AC1: an administrative-organization session below AAL2 is
    /// refused.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005b_AC1_AStaffSessionBelowAal2IsRefusedAsync()
    {
        SubjectId subject = Staff(Factor.Passkey, Factor.Password);

        Assert.Equal(
            ErrorCodes.FactorRequired,
            Refusal(await Service.BeginAsync(
                subject,
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-005b AC2: enabling a further primary factor for that organization
    /// does not lower the floor.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005b_AC2_AFurtherPrimaryFactorDoesNotLowerTheFloorAsync()
    {
        SubjectId subject = Staff(Factor.Passkey, Factor.Password, Factor.Totp);

        Assert.Equal(
            ErrorCodes.FactorRequired,
            Refusal(await Service.BeginAsync(
                subject,
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken)));
        Assert.NotNull(Value(await Service.BeginAsync(
            subject,
            [Factor.Password, Factor.Totp],
            Somewhere,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-005b AC3: a break-glass session is established despite the floor,
    /// and the audit trail records what established it.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005b_AC3_ABreakGlassSessionIsEstablishedDespiteTheFloorAsync()
    {
        SubjectId subject = Staff();

        IssuedSession issued = Value(await Service.BeginExemptAsync(
            subject,
            [Factor.BreakGlass],
            Somewhere,
            TestContext.Current.CancellationToken));

        Session session = _sessions.Behind(issued.Secret)!;

        Assert.Equal(AssuranceLevel.Aal1, session.Attained);
        Assert.True(session.SatisfiesEveryGate);
        Assert.Equal([Factor.BreakGlass], Assert.Single(_audit.Records).Presented);
    }

    /// <summary>
    /// AUTH-SESS-006 AC1 and AC2: a combination presented on a session issues a new
    /// secret and the one before it stops working.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_006_AC2_ThePreviousSecretIsInvalidatedNotOrphanedAsync()
    {
        SubjectId subject = Subject();
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        IssuedSession rotated = Value(await Service.PresentAsync(
            _sessions.Behind(issued.Secret)!,
            [Factor.Totp],
            TestContext.Current.CancellationToken));

        Assert.NotEqual(issued.Secret.Value, rotated.Secret.Value);
        Assert.NotEqual(issued.CsrfToken.Value, rotated.CsrfToken.Value);
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
        Assert.Null(await RefusalAsync(rotated.Secret));
    }

    /// <summary>
    /// AUTH-SESS-006: a privilege change issues a new identifier for the session
    /// without changing what it proved.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_006_AC1_APrivilegeChangeIssuesANewSecretAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Passkey]);
        Session session = _sessions.Behind(issued.Secret)!;

        IssuedSession rotated = Value(await Service.RotateAsync(
            session,
            TestContext.Current.CancellationToken));

        Assert.NotEqual(issued.Secret.Value, rotated.Secret.Value);
        Assert.Equal(AssuranceLevel.Aal2, _sessions.Behind(rotated.Secret)!.Attained);
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
    }

    /// <summary>
    /// AUTH-SESS-005a AC2a: presenting a second factor on a delegated session leaves
    /// it delegated.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005a_AC2a_ASecondFactorLeavesADelegatedSessionDelegatedAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Google]);
        Session session = _sessions.Behind(issued.Secret)!;

        await Service.PresentAsync(session, [Factor.Totp], TestContext.Current.CancellationToken);

        Assert.Equal(AssuranceLevel.Delegated, session.Attained);
        Assert.False(session.PhishingResistant);
    }

    /// <summary>
    /// AUTH-SESS-008 AC1: signing out everywhere leaves no session of the account
    /// active.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_008_AC1_SigningOutEverywhereLeavesNoSessionActiveAsync()
    {
        SubjectId subject = Subject();
        IssuedSession first = await BegunAsync(subject, [Factor.Password]);
        IssuedSession second = await BegunAsync(subject, [Factor.Password]);

        await Service.EndEverywhereAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(first.Secret));
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(second.Secret));
    }

    /// <summary>
    /// AUTH-SESS-013 AC1: the listing returns every live session of the account and
    /// marks exactly one as current.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_013_AC1_TheListingMarksExactlyOneSessionCurrentAsync()
    {
        SubjectId subject = Subject();
        IssuedSession first = await BegunAsync(subject, [Factor.Password]);
        IssuedSession second = await BegunAsync(subject, [Factor.Password]);
        await BegunAsync(Subject(), [Factor.Password]);

        IReadOnlyList<SessionSummary> listed = Value(await Service.ListAsync(
            AccessContext.Of(subject),
            second.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, listed.Count);
        Assert.Single(listed, summary => summary.Current);
        Assert.Contains(listed, summary => summary.Id == first.Id);
        Assert.Equal(second.Id, listed.Single(summary => summary.Current).Id);
    }

    /// <summary>
    /// AUTH-SESS-013 AC2: each entry carries the sign-in time, the last-use time, the
    /// device description and a location no finer than a city.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync()
    {
        SubjectId subject = Subject();
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        _clock.Advance(TimeSpan.FromHours(3));
        _locations.Holds("203.0.113.4", new SessionLocation("Cairo", "EG"));
        await Service.ResolveAsync(
            issued.Secret,
            new SessionOrigin("203.0.113.4", new DeviceDescription("Safari", "iOS")),
            TestContext.Current.CancellationToken);

        SessionSummary summary = Assert.Single(Value(await Service.ListAsync(
            AccessContext.Of(subject),
            issued.Id,
            TestContext.Current.CancellationToken)));

        Assert.Equal(Noon, summary.SignedInAt);
        Assert.Equal(Noon + TimeSpan.FromHours(3), summary.LastUsedAt);
        Assert.Equal(new DeviceDescription("Safari", "iOS"), summary.Device);
        Assert.Equal(new SessionLocation("Cairo", "EG"), summary.Location);
    }

    /// <summary>
    /// INT-GEN-006 AC3: the city on an entry is what the local database made of the
    /// address the session was used from, and a place written onto the origin by the
    /// code that built it is not read.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC3_TheCityIsWhatTheDatabaseMadeOfTheAddressAsync()
    {
        SubjectId subject = Subject();
        _locations.Holds("198.51.100.7", new SessionLocation("Alexandria", "EG"));

        IssuedSession issued = Value(await Service.BeginAsync(
            subject,
            [Factor.Password],
            Somewhere with { Location = new SessionLocation("Reykjavik", "IS") },
            TestContext.Current.CancellationToken));

        SessionSummary summary = Assert.Single(Value(await Service.ListAsync(
            AccessContext.Of(subject),
            issued.Id,
            TestContext.Current.CancellationToken)));

        Assert.Equal(new SessionLocation("Alexandria", "EG"), summary.Location);
        Assert.Contains("198.51.100.7", _locations.Asked, StringComparer.Ordinal);
    }

    /// <summary>
    /// INT-GEN-006 AC3: with no database to read, the session is listed without a
    /// location and nothing else about it changes.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC3_WithNoDatabaseTheSessionIsListedWithoutALocationAsync()
    {
        SubjectId subject = Subject();
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        SessionSummary summary = Assert.Single(Value(await Service.ListAsync(
            AccessContext.Of(subject),
            issued.Id,
            TestContext.Current.CancellationToken)));

        Assert.Null(summary.Location);
        Assert.Equal(Noon, summary.SignedInAt);
        Assert.Equal(new DeviceDescription("Firefox", "Fedora"), summary.Device);
    }

    /// <summary>
    /// INT-GEN-006 and CONV-DESIGN-005 AC1: a resolver that could not report what it
    /// had to report fails the sign-in, because the degradation it exists to raise is
    /// the deployment's only sight of an absent database.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AResolverThatCouldNotReportFailsTheSignInAsync()
    {
        _locations.Refusal = Error.From(ErrorCodes.SystemFault);

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await Service.BeginAsync(
                Subject(),
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-SESS-013 AC3: ending one session leaves the others intact.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_013_AC3_EndingOneSessionLeavesTheOthersIntactAsync()
    {
        SubjectId subject = Subject();
        IssuedSession ended = await BegunAsync(subject, [Factor.Password]);
        IssuedSession kept = await BegunAsync(subject, [Factor.Password]);

        await Service.EndAsync(
            AccessContext.Of(subject),
            ended.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(ended.Secret));
        Assert.Null(await RefusalAsync(kept.Secret));
    }

    /// <summary>
    /// AUTH-SESS-013: a session of another account is not the caller's to end.
    /// </summary>
    [Fact]
    public async Task EndAsync_ASessionOfAnotherAccount_IsRefusedAsync()
    {
        IssuedSession theirs = await BegunAsync(Subject(), [Factor.Password]);

        Result ended = await Service.EndAsync(
            AccessContext.Of(Subject()),
            theirs.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refusal(ended));
        Assert.Null(await RefusalAsync(theirs.Secret));
    }

    /// <summary>
    /// AUTH-SESS-011 AC1: revoking one account's sessions leaves every other session
    /// intact.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_011_AC1_RevokingOneAccountLeavesTheOthersIntactAsync()
    {
        SubjectId leaving = Subject();
        SubjectId staying = Subject();
        var organization = OrganizationId.New(_clock);
        SubjectId administrator = Subject();
        IssuedSession theirs = await BegunAsync(leaving, [Factor.Password]);
        IssuedSession others = await BegunAsync(staying, [Factor.Password]);

        _gate.Grant(administrator, organization, Permissions.SessionRevokeAccount);

        Result revoked = await Service.RevokeAccountAsync(
            AccessContext.Of(administrator),
            leaving,
            organization,
            TestContext.Current.CancellationToken);

        Assert.Null(Refusal(revoked));
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(theirs.Secret));
        Assert.Null(await RefusalAsync(others.Secret));
    }

    /// <summary>
    /// AUTH-SESS-011: per-account revocation is a permission the caller holds or it
    /// does not happen.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_011_AC2_RevokingOneAccountWithoutThePermissionIsRefusedAsync()
    {
        SubjectId leaving = Subject();
        IssuedSession theirs = await BegunAsync(leaving, [Factor.Password]);

        Result revoked = await Service.RevokeAccountAsync(
            AccessContext.Of(Subject()),
            leaving,
            OrganizationId.New(_clock),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refusal(revoked));
        Assert.Null(await RefusalAsync(theirs.Secret));
    }

    /// <summary>
    /// AUTH-SESS-009 AC3: the explicit revocation operation terminates every session.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_009_AC3_TheExplicitRevocationTerminatesEverySessionAsync()
    {
        var organization = OrganizationId.New(_clock);
        SubjectId administrator = Subject();
        IssuedSession first = await BegunAsync(Subject(), [Factor.Password]);
        IssuedSession second = await BegunAsync(Subject(), [Factor.Password]);

        _gate.Grant(administrator, organization, Permissions.SessionRevoke);

        Result revoked = await Service.RevokeEveryAsync(
            AccessContext.Of(administrator),
            organization,
            TestContext.Current.CancellationToken);

        Assert.Null(Refusal(revoked));
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(first.Secret));
        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(second.Secret));
    }

    /// <summary>
    /// AUTH-SESS-009 AC1: a policy that tightens after a session began does not end
    /// it; the session stands and is short of what the policy now requires.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_009_AC1_TighteningThePolicyEndsNoLiveSessionAsync()
    {
        SubjectId subject = Subject();
        var organization = OrganizationId.New(_clock);
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        _memberships.Place(subject, organization);
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            Tightened());

        Assert.Null(await RefusalAsync(issued.Secret));
        Assert.Equal(AssuranceLevel.Aal1, _sessions.Behind(issued.Secret)!.Attained);
    }

    /// <summary>
    /// AUTH-SESS-010 AC1: ending an account's sessions refuses the first request that
    /// reaches the record afterwards.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_AC1_EndingAnAccountRefusesTheNextRequestAsync()
    {
        SubjectId subject = Subject();
        IssuedSession issued = await BegunAsync(subject, [Factor.Password]);

        await Service.EndAccountAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(issued.Secret));
    }

    /// <summary>
    /// AUTH-PRIN-002: a factor the policy does not admit begins no session, whatever
    /// it would otherwise reach.
    /// </summary>
    [Fact]
    public async Task BeginAsync_AFactorThePolicyDoesNotAdmit_IsRefusedAsync()
    {
        Result<IssuedSession> begun = await Service.BeginAsync(
            Subject(),
            [Factor.EmailLink],
            Somewhere,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorNotPermitted, Refusal(begun));
    }

    /// <summary>
    /// CONV-DESIGN-003: an operation opens one transaction and commits it once.
    /// </summary>
    [Fact]
    public async Task BeginAsync_AnAuthentication_CommitsOneTransactionAsync()
    {
        await BegunAsync(Subject(), [Factor.Password]);

        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// AUTH-FACT-002 AC1: an entry the deployment leaves off signs nothing in until
    /// the applicable policy names it, and naming it is the whole of enabling it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC1_AnEntryOffByDefaultSignsInOnlyWhenNamedAsync()
    {
        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refusal(await Service.BeginAsync(
                Subject(),
                [Factor.EmailLink],
                Somewhere,
                TestContext.Current.CancellationToken)));

        Policy shipped = Janus.Core.Policies.SystemDefault;

        _configuration.Set(
            Settings.PolicyDefault,
            new Policy(
                shipped.RequiredAssurance,
                new HashSet<Factor>(shipped.LoginFactors) { Factor.EmailLink },
                shipped.Gates,
                shipped.CredentialRedundancy,
                shipped.SelfServiceRecovery,
                shipped.EmailDomains));

        Assert.Null(Refusal(await Service.BeginAsync(
            Subject(),
            [Factor.EmailLink],
            Somewhere,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-002 AC2: removing an entry from the policy blocks it for the
    /// organization at the next sign-in, with nothing deployed in between.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC2_RemovingAnEntryBlocksItWithoutADeployAsync()
    {
        var organization = OrganizationId.New(_clock);
        SubjectId subject = Subject();

        _memberships.Place(subject, organization);
        Admits(organization, Factor.Password, Factor.Totp);

        Assert.Null(Refusal(await Service.BeginAsync(
            subject,
            [Factor.Password, Factor.Totp],
            Somewhere,
            TestContext.Current.CancellationToken)));

        Admits(organization, Factor.Password, Factor.SecurityKey);

        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refusal(await Service.BeginAsync(
                subject,
                [Factor.Password, Factor.Totp],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-002 AC3: disabling an entry blocks the sign-in and deletes nothing,
    /// so naming it again admits the same account with no re-enrolment in between.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC3_DisablingAnEntryDeletesNoEnrolmentAsync()
    {
        var organization = OrganizationId.New(_clock);
        SubjectId subject = Subject();

        _memberships.Place(subject, organization);
        Admits(organization, Factor.Password, Factor.Totp);

        Assert.Null(Refusal(await Service.BeginAsync(
            subject,
            [Factor.Password, Factor.Totp],
            Somewhere,
            TestContext.Current.CancellationToken)));

        Admits(organization, Factor.Password);

        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refusal(await Service.BeginAsync(
                subject,
                [Factor.Password, Factor.Totp],
                Somewhere,
                TestContext.Current.CancellationToken)));

        Admits(organization, Factor.Password, Factor.Totp);

        Assert.Null(Refusal(await Service.BeginAsync(
            subject,
            [Factor.Password, Factor.Totp],
            Somewhere,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-002a AC1: a provider's word is the whole of the sign-in, so the
    /// session is usable at once and nothing further is asked.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002a_AC1_ASocialSignInYieldsAUsableSessionAtOnceAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Google]);

        Assert.Null(await RefusalAsync(issued.Secret));
        Assert.Equal([Factor.Google], Assert.Single(_audit.Records).Presented);
    }

    /// <summary>
    /// AUTH-FACT-002a AC2: a second step is second to a password, so an account that
    /// signed in on a provider's word is asked for none, whatever it has enrolled.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002a_AC2_ASocialSignInIsNoSecondStepsFirst()
    {
        Assert.False(SecondStep.Is(Factor.Google));
        Assert.False(SecondStep.Is(Factor.Apple));
        Assert.Empty(SecondStep.Offerable(
            password: false,
            FactorCatalogue.Entries.Keys.ToFrozenSet()));
    }

    /// <summary>
    /// AUTH-FACT-002a AC3: the session records no tier of ours, the provider having
    /// asserted none.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002a_AC3_TheSessionRecordsDelegatedAsync()
    {
        IssuedSession issued = await BegunAsync(Subject(), [Factor.Apple]);

        Assert.Equal(AssuranceLevel.Delegated, _sessions.Behind(issued.Secret)!.Attained);
    }

    /// <summary>
    /// AUTH-SESS-004 AC1: each application answers to its own secret, so the one
    /// taken from a compromised application opens nothing else.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_004_AC1_ACompromisedApplicationsSessionOpensNoOtherAsync()
    {
        IssuedSession record = await BegunAsync(Subject(), [Factor.Passkey]);
        IssuedSession first = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));
        IssuedSession second = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));

        Assert.NotEqual(first.Secret.Value, second.Secret.Value);
        Assert.NotEqual(_sessions.Behind(first.Secret)!.Id, _sessions.Behind(second.Secret)!.Id);
        Assert.Null(_sessions.Behind(OpaqueToken.Of(first.Secret.Value + "x")));
    }

    /// <summary>
    /// AUTH-SESS-004 AC2: the record is what the next application derives from, so
    /// arriving at it asks the person for nothing and authenticates nobody again.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_004_AC2_AnotherApplicationDerivesWithoutReauthenticatingAsync()
    {
        IssuedSession record = await BegunAsync(Subject(), [Factor.Passkey]);

        _audit.Records.Clear();

        IssuedSession app = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));

        Assert.Null(await RefusalAsync(app.Secret));
        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-SESS-005 AC6: an organization whose policy asks for the higher tier gets
    /// the shorter pair of lifetimes, and a principal outside it keeps the longer.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC6_AnOrganizationsShorterTimeoutAppliesAsync()
    {
        IssuedSession shorter = await BegunAsync(Staff(), [Factor.Passkey]);
        IssuedSession longer = await BegunAsync(Subject(), [Factor.Passkey]);

        Assert.Equal(
            Noon + Settings.SessionAal2Inactivity.Default,
            _sessions.Behind(shorter.Secret)!.IdleExpiry);
        Assert.Equal(
            Noon + Settings.SessionDefaultInactivity.Default,
            _sessions.Behind(longer.Secret)!.IdleExpiry);
    }

    /// <summary>
    /// AUTH-SESS-008 AC2: the record every application stands on is gone too, so the
    /// next application signs nobody back in.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_008_AC2_AnotherApplicationRequiresAuthenticationAfterLogoutAsync()
    {
        SubjectId subject = Subject();
        IssuedSession record = await BegunAsync(subject, [Factor.Password]);
        IssuedSession app = Value(await Service.DeriveAsync(
            record.Id,
            SessionType.PerApp,
            Somewhere,
            TestContext.Current.CancellationToken));

        await Service.EndEverywhereAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SessionExpired, await RefusalAsync(app.Secret));
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refusal(await Service.DeriveAsync(
                record.Id,
                SessionType.PerApp,
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    private static PolicyOverride Tightened() =>
        new(
            AssuranceLevel.Aal2,
            null,
            null,
            null,
            null,
            null);

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode? Refusal(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

    private static ErrorCode? Refusal<TValue>(Result<TValue> result) =>
        result.Match<ErrorCode?>(_ => null, error => error.Code);

    private SubjectId Subject() => SubjectId.New(_randomness);

    private void Admits(OrganizationId organization, params Factor[] factors) =>
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            new PolicyOverride(null, factors.ToFrozenSet(), null, null, null, null));

    private SubjectId Staff(params Factor[] factors)
    {
        SubjectId subject = Subject();
        var organization = OrganizationId.New(_clock);

        _memberships.Place(subject, organization);
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            new PolicyOverride(
                AssuranceLevel.Aal2,
                factors.Length == 0 ? null : factors.ToFrozenSet(),
                null,
                null,
                null,
                null));

        return subject;
    }

    private async ValueTask<IssuedSession> BegunAsync(SubjectId subject, Factor[] presented)
    {
        _work.Reset();

        return Value(await Service.BeginAsync(
            subject,
            presented,
            Somewhere,
            TestContext.Current.CancellationToken));
    }

    private async ValueTask<ErrorCode?> RefusalAsync(OpaqueToken secret) =>
        Refusal(await Service.ResolveAsync(
            secret,
            Somewhere,
            TestContext.Current.CancellationToken));

    private async ValueTask<string?> AskedAsync(OpaqueToken secret) =>
        (await Service.ResolveAsync(secret, Somewhere, TestContext.Current.CancellationToken))
            .Match<string?>(
                _ => null,
                error => error.Details.TryGetValue("reauthenticate", out System.Text.Json.JsonElement asked)
                    ? asked.GetString()
                    : null);
}
