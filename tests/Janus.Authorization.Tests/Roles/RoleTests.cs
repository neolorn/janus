using System;
using Janus.Authorization.Roles;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Roles;

/// <summary>
/// The bundle of permissions a grant names (chapter 10 section 3).
/// </summary>
[Trait("kind", "unit")]
public sealed class RoleTests
{
    /// <summary>
    /// A role allows each permission it was given and nothing else.
    /// </summary>
    [Fact]
    public void Of_ThePermissionsGiven_AllowsEachOfThemAndNothingElse()
    {
        var role = Role.Of(
            RoleName.Parse("reader"),
            [Permissions.GrantRead, Permissions.AuditRead]);

        Assert.True(role.Allows(Permissions.GrantRead));
        Assert.True(role.Allows(Permissions.AuditRead));
        Assert.False(role.Allows(Permissions.GrantManage));
    }

    /// <summary>
    /// A role given nothing allows nothing.
    /// </summary>
    [Fact]
    public void Of_NoPermissions_AllowsNothing()
    {
        var role = Role.Of(RoleName.Parse("bystander"), []);

        Assert.Empty(role.Permissions);
        Assert.False(role.Allows(Permissions.GrantRead));
    }

    /// <summary>
    /// The same permission twice is the same permission.
    /// </summary>
    [Fact]
    public void Of_TheSamePermissionTwice_HoldsItOnce()
    {
        var role = Role.Of(
            RoleName.Parse("reader"),
            [Permissions.GrantRead, Permissions.GrantRead]);

        Assert.Single(role.Permissions);
    }

    /// <summary>
    /// What a role allows is edited on the role, which is what makes it data.
    /// </summary>
    [Fact]
    public void Allow_APermissionTheRoleLacked_AllowsIt()
    {
        var role = Role.Of(RoleName.Parse("reader"), [Permissions.GrantRead]);

        role.Allow(Permissions.AuditRead);

        Assert.True(role.Allows(Permissions.AuditRead));
    }

    /// <summary>
    /// A permission taken out of a role is no longer allowed by it.
    /// </summary>
    [Fact]
    public void Disallow_APermissionTheRoleAllowed_NoLongerAllowsIt()
    {
        var role = Role.Of(
            RoleName.Parse("reader"),
            [Permissions.GrantRead, Permissions.AuditRead]);

        role.Disallow(Permissions.AuditRead);

        Assert.False(role.Allows(Permissions.AuditRead));
        Assert.True(role.Allows(Permissions.GrantRead));
    }

    /// <summary>
    /// Absent permissions are a fault in the caller, not a role that allows nothing.
    /// </summary>
    [Fact]
    public void Of_WithoutPermissions_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Role.Of(RoleName.Parse("reader"), null!));
}
