using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The organization identifier: the version and the ordering CONV-DESIGN-004 asks of
/// every identifier but the subject's.
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationIdTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// CONV-DESIGN-004: every identifier but the subject's is a version 7 value.
    /// </summary>
    [Fact]
    public void New_AnyClock_ProducesAVersionSevenValue()
    {
        var organization = OrganizationId.New(new FixedClock(Noon));

        Assert.Equal(7, organization.Value.Version);
        Assert.Equal(0x80, organization.Value.ToByteArray(bigEndian: true)[8] & 0xC0);
    }

    /// <summary>
    /// CONV-DESIGN-004: a row written later sorts later in the database's own order,
    /// which is the index locality the version is chosen for.
    /// </summary>
    [Fact]
    public void New_ALaterInstant_SortsAfterAnEarlierOne()
    {
        var clock = new FixedClock(Noon);
        var earlier = OrganizationId.New(clock);

        clock.Advance(TimeSpan.FromSeconds(1));
        var later = OrganizationId.New(clock);

        Assert.True(earlier.Value.ToByteArray(bigEndian: true)
            .AsSpan()
            .SequenceCompareTo(later.Value.ToByteArray(bigEndian: true)) < 0);
    }

    /// <summary>
    /// CONV-CODE-006: the generator refuses a clock it was not given.
    /// </summary>
    [Fact]
    public void New_NoClock_Throws() =>
        Assert.Throws<ArgumentNullException>(() => OrganizationId.New(null!));
}
