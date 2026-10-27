using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests.Configuration;

/// <summary>
/// Which way a change loosens, as the chapter 10 section 4 direction paragraph reads it
/// and as OPS-CFG-002 charges for it.
/// </summary>
[Trait("kind", "unit")]
public sealed class SettingDirectionTests
{
    /// <summary>
    /// Chapter 10 section 4: a key bounded only above loosens upward, so a longer
    /// session lifetime loosens and a shorter one does not.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_AKeyBoundedOnlyAboveLoosensUpward()
    {
        Assert.Equal(SettingDirection.Increase, Settings.SessionAal2Inactivity.Loosening);
        Assert.True(Settings.SessionAal2Inactivity.Loosens(TimeSpan.FromHours(1), TimeSpan.FromHours(2)));
        Assert.False(Settings.SessionAal2Inactivity.Loosens(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));
        Assert.False(Settings.SessionAal2Inactivity.Loosens(TimeSpan.FromHours(1), TimeSpan.FromHours(1)));
    }

    /// <summary>
    /// Chapter 10 section 4: a key bounded only below loosens downward, so a shorter
    /// password floor loosens and a longer one does not.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_AKeyBoundedOnlyBelowLoosensDownward()
    {
        Assert.Equal(SettingDirection.Decrease, Settings.PasswordFloorWithMfa.Loosening);
        Assert.True(Settings.PasswordFloorWithMfa.Loosens(12, 9));
        Assert.False(Settings.PasswordFloorWithMfa.Loosens(12, 16));
    }

    /// <summary>
    /// Chapter 10 section 4: where a row names the direction, that governs. The cadence
    /// of the signing keys is bounded only below and yet loosens when lengthened, since
    /// a longer cadence leaves a key and a client secret in use for longer.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_TheSigningRotationLoosensWhenLengthened()
    {
        Assert.Equal(SettingDirection.Increase, Settings.TokenSigningRotation.Loosening);
        Assert.True(Settings.TokenSigningRotation.Loosens(TimeSpan.FromDays(90), TimeSpan.FromDays(180)));
        Assert.False(Settings.TokenSigningRotation.Loosens(TimeSpan.FromDays(90), TimeSpan.FromDays(30)));
        Assert.False(Settings.TokenSigningRotation.Loosens(TimeSpan.FromDays(90), TimeSpan.FromDays(90)));
    }

    /// <summary>
    /// Chapter 10 section 4: a boolean loosens away from its default, so turning off a
    /// check that ships on loosens and turning it back on does not.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_ABooleanLoosensAwayFromItsDefault()
    {
        Assert.True(Settings.DeviceVerificationEnabled.Default);
        Assert.True(Settings.DeviceVerificationEnabled.Loosens(true, false));
        Assert.False(Settings.DeviceVerificationEnabled.Loosens(false, true));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-212): handing a role out by holding its permissions ships off,
    /// and turning it on loosens.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_005_HandingOutByHoldingShipsOffAndTurningItOnLoosens()
    {
        Assert.False(Settings.AuthzHandoutByHolding.Default);
        Assert.Equal(SettingScope.Runtime, Settings.AuthzHandoutByHolding.Scope);
        Assert.True(Settings.AuthzHandoutByHolding.Loosens(false, true));
        Assert.False(Settings.AuthzHandoutByHolding.Loosens(true, false));
    }

    /// <summary>
    /// Chapter 10 section 4: a set loosens by what it gained or lost, and this one by
    /// what it lost, so dropping a rejection source loosens and adding one does not.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_ASetLoosensByTheMemberItLost()
    {
        IReadOnlySet<BlocklistRejectionSource> both = new HashSet<BlocklistRejectionSource>
        {
            BlocklistRejectionSource.Leaked,
            BlocklistRejectionSource.Context,
        };

        IReadOnlySet<BlocklistRejectionSource> one = new HashSet<BlocklistRejectionSource>
        {
            BlocklistRejectionSource.Leaked,
        };

        Assert.Equal(SettingDirection.Decrease, Settings.PasswordBlocklistSources.Loosening);
        Assert.True(Settings.PasswordBlocklistSources.Loosens(both, one));
        Assert.False(Settings.PasswordBlocklistSources.Loosens(one, both));
    }

    /// <summary>
    /// Chapter 10 section 4: where a key's own chapter names the direction, that
    /// governs. The restriction set loosens where a restriction was deleted or replaced
    /// by one that lets more through, and a set that only gains one does not
    /// (AUTH-ABUSE-004).
    /// </summary>
    [Fact]
    public void OPS_CFG_002_TheRestrictionSetLoosensByItsOwnRule()
    {
        IReadOnlyList<Restriction> standing = [Restricted(3, TimeSpan.FromHours(24))];

        Assert.False(Settings.Restrictions.Loosens(standing, [Restricted(2, TimeSpan.FromHours(24))]));
        Assert.False(Settings.Restrictions.Loosens(standing, [Restricted(3, TimeSpan.FromHours(48))]));
        Assert.True(Settings.Restrictions.Loosens(standing, [Restricted(4, TimeSpan.FromHours(24))]));
        Assert.True(Settings.Restrictions.Loosens(standing, [Restricted(3, TimeSpan.FromHours(1))]));
        Assert.True(Settings.Restrictions.Loosens(standing, []));
        Assert.False(Settings.Restrictions.Loosens([], standing));
    }

    private static Restriction Restricted(int maximum, TimeSpan interval) =>
        new(
            "sms.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [new Bucket(maximum, interval, BucketWindow.Sliding)]);
}
