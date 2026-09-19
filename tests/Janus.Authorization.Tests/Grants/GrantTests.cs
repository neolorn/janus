using System;
using Janus.Authorization.Grants;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// The sentence a permission is, and what it records (AUTHZ-GRANT-001, AUTHZ-GRANT-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class GrantTests
{
    private static readonly DateTimeOffset Granted = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// AUTHZ-GRANT-001 AC2: a grant naming no record is a grant on the organization.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganization()
    {
        Result<Grant> created = Create(on: null);

        Grant grant = Outcome.Value(created);

        Assert.True(grant.IsOrganizationWide);
        Assert.Null(grant.ResourceType);
        Assert.Null(grant.ResourceId);
    }

    /// <summary>
    /// AUTHZ-GRANT-001 AC3: the same row carries a grant held by an account and a
    /// grant held by a group, the subject saying which.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_001_AC3_OneShapeCarriesAccountAndGroupGrants()
    {
        Grant held = Outcome.Value(Create(on: null));
        Grant collective = Outcome.Value(Create(on: null, subject: GrantSubject.Of(Identifiers.Group())));

        Assert.Equal(SubjectType.User, held.Subject.Type);
        Assert.Equal(SubjectType.Group, collective.Subject.Type);
        Assert.Equal(held.GetType(), collective.GetType());
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC2: creation records who granted, when and why.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_003_AC2_CreationRecordsWhoWhenAndWhy()
    {
        SubjectId granter = Identifiers.Subject();

        Grant grant = Outcome.Value(Create(on: null, grantedBy: granter));

        Assert.Equal(granter, grant.GrantedBy);
        Assert.Equal(Granted, grant.GrantedAt);
        Assert.Equal("covering the quarter close", grant.Reason);
        Assert.Null(grant.RevokedBy);
        Assert.Null(grant.RevokedAt);
        Assert.Null(grant.RevocationReason);
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC2: revocation records who revoked, when and why.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_003_AC2_RevocationRecordsWhoWhenAndWhy()
    {
        Grant grant = Outcome.Value(Create(on: null));
        SubjectId revoker = Identifiers.Subject();
        DateTimeOffset revoked = Granted.AddDays(4);

        Result outcome = grant.Revoke(revoker, revoked, "  the engagement ended  ");

        Assert.True(outcome.Match(() => true, _ => false));
        Assert.Equal(revoker, grant.RevokedBy);
        Assert.Equal(revoked, grant.RevokedAt);
        Assert.Equal("the engagement ended", grant.RevocationReason);
    }

    /// <summary>
    /// D-153: a grant without a stated reason is refused by name.
    /// </summary>
    /// <param name="reason">A reason that states nothing.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithoutAStatedReason_RefusedWithReasonRequired(string reason)
    {
        Result<Grant> created = Create(on: null, reason: reason);

        Assert.Equal(ErrorCodes.GrantReasonRequired, Outcome.Code(created));
    }

    /// <summary>
    /// D-153: a revocation without a stated reason is refused by name, and the grant
    /// stands.
    /// </summary>
    [Fact]
    public void Revoke_WithoutAStatedReason_RefusedWithReasonRequired()
    {
        Grant grant = Outcome.Value(Create(on: null));

        Result outcome = grant.Revoke(Identifiers.Subject(), Granted, "   ");

        Assert.Equal(ErrorCodes.GrantReasonRequired, Outcome.Code(outcome));
        Assert.Null(grant.RevokedAt);
        Assert.True(grant.IsLive(Granted));
    }

    /// <summary>
    /// API-CONV-002: past the free-text length the boundary enforces, a value of
    /// another length is a fault rather than a refusal.
    /// </summary>
    [Fact]
    public void Create_WithAReasonPastTheFreeTextLength_Throws() =>
        Assert.Throws<ArgumentException>(() => Create(on: null, reason: new string('r', 1025)));

    /// <summary>
    /// A grant is revoked once; revoking it again is a fault in the caller.
    /// </summary>
    [Fact]
    public void Revoke_AGrantAlreadyRevoked_Throws()
    {
        Grant grant = Outcome.Value(Create(on: null));
        grant.Revoke(Identifiers.Subject(), Granted, "the engagement ended");

        Assert.Throws<InvalidOperationException>(
            () => grant.Revoke(Identifiers.Subject(), Granted, "again"));
    }

    /// <summary>
    /// A grant given no expiry confers what it confers until it is revoked.
    /// </summary>
    [Fact]
    public void IsLive_WithNoExpiry_IsLive()
    {
        Grant grant = Outcome.Value(Create(on: null));

        Assert.True(grant.IsLive(Granted.AddYears(20)));
    }

    /// <summary>
    /// Expiry is read where the grant is read, so the instant decides and no sweep is
    /// involved.
    /// </summary>
    /// <param name="days">How far past the grant the question is asked.</param>
    /// <param name="live">Whether the grant is live then.</param>
    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    public void IsLive_AroundItsExpiry_FollowsTheInstant(int days, bool live)
    {
        Grant grant = Outcome.Value(Create(on: null, expiresAt: Granted.AddDays(7)));

        Assert.Equal(live, grant.IsLive(Granted.AddDays(days)));
    }

    /// <summary>
    /// A revoked grant is live at no instant.
    /// </summary>
    [Fact]
    public void IsLive_AfterRevocation_IsNotLive()
    {
        Grant grant = Outcome.Value(Create(on: null));
        grant.Revoke(Identifiers.Subject(), Granted.AddDays(1), "the engagement ended");

        Assert.False(grant.IsLive(Granted.AddDays(2)));
        Assert.False(grant.IsLive(Granted));
    }

    /// <summary>
    /// A row read back carries what was written to it.
    /// </summary>
    [Fact]
    public void Existing_ARevokedRow_CarriesItsRevocation()
    {
        SubjectId revoker = Identifiers.Subject();

        var grant = Grant.Existing(
            Identifiers.Grant(),
            GrantSubject.Of(Identifiers.Subject()),
            RoleName.Parse("reader"),
            Identifiers.Organization(),
            Identifiers.Resource("document"),
            deny: false,
            GrantKind.Stored,
            expiresAt: null,
            Identifiers.Subject(),
            Granted,
            "covering the quarter close",
            revoker,
            Granted.AddDays(1),
            "the engagement ended");

        Assert.Equal(revoker, grant.RevokedBy);
        Assert.False(grant.IsLive(Granted));
        Assert.False(grant.IsOrganizationWide);
    }

    private static Result<Grant> Create(
        ResourceReference? on,
        GrantSubject? subject = null,
        SubjectId? grantedBy = null,
        DateTimeOffset? expiresAt = null,
        string reason = "covering the quarter close") =>
        Grant.Create(
            Identifiers.Grant(),
            subject ?? GrantSubject.Of(Identifiers.Subject()),
            RoleName.Parse("reader"),
            Identifiers.Organization(),
            on,
            deny: false,
            GrantKind.Stored,
            expiresAt,
            grantedBy ?? Identifiers.Subject(),
            Granted,
            reason);
}
