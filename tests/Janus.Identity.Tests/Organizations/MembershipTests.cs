using System;
using Janus.Core;
using Janus.Identity.Organizations;
using Xunit;

namespace Janus.Identity.Tests.Organizations;

/// <summary>
/// A membership and the lifecycle it carries of its own (IDN-MEM-001, IDN-PRIN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class MembershipTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly MembershipId Id =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly OrganizationId Acme =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    /// <summary>
    /// IDN-MEM-001 AC2: the record carries when it began and, once it has ended, when
    /// it ended.
    /// </summary>
    [Fact]
    public void IDN_MEM_001_AC2_TheRecordCarriesItsBeginningAndItsEnd()
    {
        var membership = Membership.Create(Id, Ahmed, Acme, Noon);

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
        var membership = Membership.Create(Id, Ahmed, Acme, Noon);

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
        var membership = Membership.Create(Id, Ahmed, Acme, Noon);
        membership.End(Noon.AddDays(400));

        Assert.Throws<InvalidOperationException>(() => membership.End(Noon.AddDays(500)));
    }
}
