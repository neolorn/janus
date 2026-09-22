using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Identity.Organizations;
using Xunit;
using Xunit.Sdk;

namespace Janus.Identity.Tests.Organizations;

/// <summary>
/// A membership, the lifecycle it carries of its own, and how many an account may hold
/// at once (IDN-MEM-001, IDN-MEM-002, IDN-PRIN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class MembershipTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly MembershipId Id =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly MembershipId Second =
        new(Guid.Parse("44444444-4444-4444-8444-444444444445"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Fatma =
        new(Guid.Parse("11111111-1111-4111-8111-111111111112"));

    private static readonly OrganizationId Acme =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Beta =
        new(Guid.Parse("33333333-3333-4333-8333-333333333334"));

    /// <summary>
    /// IDN-MEM-001 AC2: the record carries when it began and, once it has ended, when
    /// it ended.
    /// </summary>
    [Fact]
    public void IDN_MEM_001_AC2_TheRecordCarriesItsBeginningAndItsEnd()
    {
        Membership membership = Made(Id, Acme);

        Assert.Equal(Noon, membership.CreatedAt);
        Assert.Null(membership.EndedAt);
        Assert.True(membership.IsCurrent);

        DateTimeOffset later = Noon.AddDays(400);
        membership.End(later);

        Assert.Equal(later, membership.EndedAt);
        Assert.False(membership.IsCurrent);
    }

    /// <summary>
    /// IDN-MEM-001 AC1, IDN-PRIN-003: ending a membership changes the membership and
    /// nothing else. The record goes on naming both sides.
    /// </summary>
    [Fact]
    public void IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesNamed()
    {
        Membership membership = Made(Id, Acme);

        membership.End(Noon.AddDays(400));

        Assert.Equal(Ahmed, membership.Subject);
        Assert.Equal(Acme, membership.Organization);
    }

    /// <summary>
    /// A membership ends once. Ending it twice would move the instant it ended.
    /// </summary>
    [Fact]
    public void End_AMembershipThatHasEnded_Throws()
    {
        Membership membership = Made(Id, Acme);
        membership.End(Noon.AddDays(400));

        Assert.Throws<InvalidOperationException>(() => membership.End(Noon.AddDays(500)));
    }

    /// <summary>
    /// IDN-MEM-002 AC2: the deployment allows one membership unless it says otherwise,
    /// so a second one is refused by name and nothing is made.
    /// </summary>
    [Fact]
    public void IDN_MEM_002_AC2_ASecondMembershipIsRefusedByDefault()
    {
        Membership held = Made(Id, Acme);

        Result<Membership> second = Membership.Create(
            Second,
            Ahmed,
            Beta,
            [held],
            multiple: false,
            Noon.AddDays(1));

        Assert.Equal(
            ErrorCodes.MembershipLimitReached,
            second.Match(_ => null, error => (ErrorCode?)error.Code));
    }

    /// <summary>
    /// IDN-MEM-002 AC3: the same account and the same two organizations, with the
    /// setting enabled, holds both. Nothing about the record changed.
    /// </summary>
    [Fact]
    public void IDN_MEM_002_AC3_TheSettingAloneAdmitsTheSecondMembership()
    {
        Membership held = Made(Id, Acme);

        Membership second = Membership
            .Create(Second, Ahmed, Beta, [held], multiple: true, Noon.AddDays(1))
            .Match(made => made, error => throw new XunitException(error.Code.ToString()));

        Assert.Equal(Beta, second.Organization);
        Assert.True(second.IsCurrent);
    }

    /// <summary>
    /// IDN-MEM-002 AC2: a membership the account ended is not one it holds, so the
    /// account joins again on the deployment's default.
    /// </summary>
    [Fact]
    public void IDN_MEM_002_AC2_AMembershipThatEndedLeavesRoomForAnother()
    {
        Membership ended = Made(Id, Acme);
        ended.End(Noon.AddDays(400));

        Membership again = Membership
            .Create(Second, Ahmed, Acme, [ended], multiple: false, Noon.AddDays(401))
            .Match(made => made, error => throw new XunitException(error.Code.ToString()));

        Assert.Equal(Acme, again.Organization);
    }

    /// <summary>
    /// IDN-MEM-002: one membership of an organization is one membership of it, so a
    /// second of the same organization is refused whatever the setting says, and the
    /// refusal names the organization.
    /// </summary>
    [Fact]
    public void IDN_MEM_002_ASecondMembershipOfTheSameOrganizationIsRefused()
    {
        Membership held = Made(Id, Acme);

        Result<Membership> again = Membership.Create(
            Second,
            Ahmed,
            Acme,
            [held],
            multiple: true,
            Noon.AddDays(1));

        Error? refusal = again.Match(_ => null, error => (Error?)error);

        Assert.Equal(ErrorCodes.MembershipLimitReached, refusal?.Code);
        Assert.Equal(Acme.Value.ToString(), refusal?.Details["organization"].GetString());
    }

    /// <summary>
    /// Another account's memberships decide nothing about this one, and a caller that
    /// hands them over has made a mistake the rule cannot see past.
    /// </summary>
    [Fact]
    public void Create_MembershipsOfAnotherAccount_Throws()
    {
        Membership held = Made(Id, Acme);

        Assert.Throws<ArgumentException>(() => Membership.Create(
            Second,
            Fatma,
            Beta,
            [held],
            multiple: false,
            Noon.AddDays(1)));
    }

    // The account holds nothing yet, which is the state every case here begins from.
    private static Membership Made(MembershipId id, OrganizationId organization) =>
        Membership
            .Create(id, Ahmed, organization, [], multiple: false, Noon)
            .Match(made => made, error => throw new XunitException(error.Code.ToString()));
}
