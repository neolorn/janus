using System.Collections.Generic;
using System.Linq;
using Janus.Authorization.Roles;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Roles;

/// <summary>
/// What a role carries and hands out once its inclusions are followed (AUTHZ-GRANT-006,
/// D-278).
/// </summary>
[Trait("kind", "unit")]
public sealed class RoleGraphTests
{
    private static readonly Permission Read = Permission.Parse("article:read");

    private static readonly Permission Edit = Permission.Parse("article:edit");

    private static readonly Permission Approve = Permission.Parse("article:approve");

    private static readonly RoleName Owner = RoleName.Parse("owner");

    private static readonly RoleName Manager = RoleName.Parse("manager");

    private static readonly RoleName Clerk = RoleName.Parse("clerk");

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly RoleName Intern = RoleName.Parse("intern");

    /// <summary>
    /// AUTHZ-GRANT-006 AC1: a role including another, which includes a third, carries the
    /// third's permissions and hands out the roles its <c>handsOut</c> names.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC1_ARoleCarriesWhatItIncludesTwoLevelsDown()
    {
        RoleGraph graph = Graph(
            [Role.Of(Owner, []), Role.Of(Manager, [Edit]), Role.Of(Clerk, [Read], [Intern]), Role.Of(Intern, [])],
            [(Owner, Included(Manager)), (Manager, Included(Clerk))]);

        Assert.Equal([Edit, Read], Sorted(graph.Permissions(Owner).Keys));
        Assert.Equal([Intern], graph.HandsOut(Owner).Keys);
        Assert.Equal([Manager, Clerk], graph.Permissions(Owner)[Read]);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC2: an action declared not inherited passes through no inclusion,
    /// and the role naming it still carries it.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC2_AnActionNotInheritedStaysWithTheRoleNamingIt()
    {
        RoleGraph graph = Graph(
            [Role.Of(Manager, []), Role.Of(Clerk, [Read, Approve])],
            [(Manager, Included(Clerk))],
            uninherited: [Approve]);

        Assert.Equal([Read], graph.Permissions(Manager).Keys);
        Assert.Equal([Approve, Read], Sorted(graph.Permissions(Clerk).Keys));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC3: a permission and a role an inclusion leaves out are not passed
    /// on through it, and stay with the included role.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC3_WhatAnInclusionLeavesOutIsNotPassedOn()
    {
        RoleGraph graph = Graph(
            [Role.Of(Manager, []), Role.Of(Clerk, [Read, Edit], [Intern, Reader]), Role.Of(Intern, []), Role.Of(Reader, [])],
            [(Manager, new RoleInclusion(Clerk, Enabled: true, [Edit], [Intern]))]);

        Assert.Equal([Read], graph.Permissions(Manager).Keys);
        Assert.Equal([Reader], graph.HandsOut(Manager).Keys);
        Assert.Equal([Edit, Read], Sorted(graph.Permissions(Clerk).Keys));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC5: with the key off each role carries its own alone; an inclusion
    /// switched off passes nothing on and the others still do.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC5_TheKeyAndAnInclusionsSwitchEachTurnInclusionsOff()
    {
        Role[] roles = [Role.Of(Manager, [Approve]), Role.Of(Clerk, [Read]), Role.Of(Reader, [Edit])];
        (RoleName, RoleInclusion)[] inclusions =
        [
            (Manager, Included(Clerk)),
            (Manager, new RoleInclusion(Reader, Enabled: false, [], [])),
        ];

        Assert.Equal([Approve], Graph(roles, inclusions, enabled: false).Permissions(Manager).Keys);
        Assert.Equal([Approve, Read], Sorted(Graph(roles, inclusions).Permissions(Manager).Keys));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC8, OPS-CFG-007: system administration is never passed on through
    /// an inclusion.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC8_SystemAdministrationIsNeverPassedOn()
    {
        RoleGraph graph = Graph(
            [Role.Of(Manager, []), Role.Of(Clerk, [Read, Permissions.SystemAdminister])],
            [(Manager, Included(Clerk))]);

        Assert.Equal([Read], graph.Permissions(Manager).Keys);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC9 (D-278): each entry is listed once, with its source the role's
    /// own where it carries it itself, otherwise the shortest path, and among paths of one
    /// length the first by the ordinal order of the names along it, nearest first.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC9_EachEntryIsListedOnceWithItsSource()
    {
        RoleGraph graph = Graph(
            [
                Role.Of(Owner, [Edit]),
                Role.Of(Manager, []),
                Role.Of(Clerk, [Read, Edit]),
                Role.Of(Reader, [Read]),
                Role.Of(Intern, [Read, Approve]),
            ],
            [
                (Owner, Included(Reader)),
                (Owner, Included(Manager)),
                (Manager, Included(Clerk)),
                (Owner, Included(Intern)),
            ]);

        EffectiveRole effective = graph.Effective(Owner);

        Assert.Equal(
            [("article:approve", "intern"), ("article:edit", string.Empty), ("article:read", "intern")],
            effective.Permissions.Select(entry => (entry.Name.ToString(), string.Join('/', entry.From))));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC6: whether a role reaches another follows every inclusion, on or
    /// off, at any depth, which is what a loop is judged on.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_006_AC6_ARoleReachesWhatItIncludesAtAnyDepthOnOrOff()
    {
        RoleGraph graph = Graph(
            [Role.Of(Owner, []), Role.Of(Manager, []), Role.Of(Clerk, [])],
            [(Owner, Included(Manager)), (Manager, new RoleInclusion(Clerk, Enabled: false, [], []))]);

        Assert.True(graph.Reaches(Owner, Clerk));
        Assert.False(graph.Reaches(Clerk, Owner));
        Assert.True(graph.With(Clerk, Included(Owner)).Reaches(Owner, Owner));
        Assert.False(graph.Without(Manager, Clerk).Reaches(Owner, Clerk));
    }

    private static RoleInclusion Included(RoleName role) => new(role, Enabled: true, [], []);

    private static RoleGraph Graph(
        IEnumerable<Role> roles,
        IEnumerable<(RoleName, RoleInclusion)> inclusions,
        HashSet<Permission>? uninherited = null,
        bool enabled = true) =>
        RoleGraph.Of(roles, inclusions, uninherited ?? [], enabled);

    private static List<Permission> Sorted(IEnumerable<Permission> permissions) =>
        [.. permissions.OrderBy(permission => permission.ToString(), System.StringComparer.Ordinal)];
}
