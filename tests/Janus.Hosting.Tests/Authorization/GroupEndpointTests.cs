using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Groups;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The groups of an organization over <c>/admin/groups</c> of chapter 09 section 8a,
/// behind <c>group:manage</c> in the group's organization, with a change of members
/// stepped up as a grant is (AUTHZ-GROUP-001, OPS-CFG-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class GroupEndpointTests : IAsyncLifetime
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    private static readonly Guid Holder = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public GroupEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <summary>
    /// A reading role and the seeded administrative role.
    /// </summary>
    /// <returns>The work of writing them.</returns>
    public async ValueTask InitializeAsync()
    {
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Permission.Parse("document:read")]), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(SystemAdministrator, Permissions.All), CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GROUP-001: a group is created in an organization, given a member, and read
    /// back with it; each change is written down with who, which group and why.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AGroupIsCreatedAndReadWithItsMembersAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Branch, Permissions.GroupManage);

        Answer created = await CreatedAsync(administrator, "  Tellers  ", reason: "  Counter staff.  ");
        GroupId tellers = Id(created);
        Answer added = await AddedAsync(administrator, tellers, User);
        Answer read = await administrator.SendAsync("GET", "/admin/groups?organization=" + Branch);

        Assert.Equal(StatusCodes.Status204NoContent, added.Status);
        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement group = Assert.Single(read.Json().EnumerateArray());
        JsonElement member = Assert.Single(group.GetProperty("members").EnumerateArray());

        Assert.Equal(tellers.ToString(), group.GetProperty("id").GetString());
        Assert.Equal("Tellers", group.GetProperty("name").GetString());
        Assert.Equal("user", member.GetProperty("subjectType").GetString());
        Assert.Equal(Holder, member.GetProperty("subjectId").GetGuid());

        Assert.Collection(
            _deployment.GroupChanges.Changes,
            change =>
            {
                Assert.Equal(AuditActions.GroupCreated, change.Action);
                Assert.Equal(tellers, change.Group);
                Assert.Equal(Branch, change.Organization);
                Assert.Null(change.Member);
                Assert.Equal("Counter staff.", change.Reason);
                Assert.Equal(actor, change.Actor);
            },
            change =>
            {
                Assert.Equal(AuditActions.GroupMemberAdded, change.Action);
                Assert.Equal(User, change.Member);
                Assert.Equal(actor, change.Actor);
            });
    }

    /// <summary>
    /// AUTHZ-GROUP-001 AC1: a team added to a department passes the department's
    /// membership on to the team's own members.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AC1_GroupsNestThroughTheMembersRouteAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId department = Id(await CreatedAsync(administrator, "Department"));
        GroupId team = Id(await CreatedAsync(administrator, "Team"));

        Answer nested = await AddedAsync(administrator, department, GrantSubject.Of(team));
        Answer joined = await AddedAsync(administrator, team, User);

        Assert.Equal(StatusCodes.Status204NoContent, nested.Status);
        Assert.Equal(StatusCodes.Status204NoContent, joined.Status);
        Assert.Equal(
            new[] { department.Value, team.Value }.Order(),
            (await _deployment.Groups.GroupsOfAsync(User, CancellationToken.None)).Select(id => id.Value).Order());
    }

    /// <summary>
    /// AUTHZ-GROUP-001: a group that would come to contain itself, directly or through
    /// a group it holds, is refused with <c>authz.group.cycle</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AGroupThatWouldContainItselfIsRefusedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId department = Id(await CreatedAsync(administrator, "Department"));
        GroupId team = Id(await CreatedAsync(administrator, "Team"));

        _ = await AddedAsync(administrator, department, GrantSubject.Of(team));

        Answer around = await AddedAsync(administrator, team, GrantSubject.Of(department));
        Answer itself = await AddedAsync(administrator, team, GrantSubject.Of(team));

        Assert.Equal(StatusCodes.Status409Conflict, around.Status);
        Assert.Equal(ErrorCodes.GroupCycle.ToString(), around.Text("code"));
        Assert.Equal(StatusCodes.Status409Conflict, itself.Status);
        Assert.Equal(ErrorCodes.GroupCycle.ToString(), itself.Text("code"));
        Assert.Empty(await _deployment.Groups.MembersAsync(team, CancellationToken.None));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 and AUTHZ-CONCEAL-005: <c>group:manage</c> is asked in the
    /// organization the group belongs to, so holding it in another organization reads
    /// and changes nothing here.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_GroupManageIsAskedInTheGroupsOrganizationAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration, Permissions.GroupManage);
        Group tellers = await HeldAsync(Branch, "Tellers");

        Answer read = await administrator.SendAsync("GET", "/admin/groups?organization=" + Branch);
        Answer created = await CreatedAsync(administrator, "Auditors");
        Answer added = await AddedAsync(administrator, tellers.Id, User);
        Answer removed = await RemovedAsync(administrator, tellers.Id);

        foreach (Answer refused in (Answer[])[read, created, added, removed])
        {
            Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
            Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        }

        Assert.Single(await _deployment.Groups.InAsync(Branch, CancellationToken.None));
        Assert.Empty(_deployment.GroupChanges.Changes);
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3 and AUTHZ-SCOPE-001: a change to a group meets the gate in
    /// the group's organization before the group is read, so a caller without
    /// <c>group:manage</c> there is refused alike whether or not the group exists, and
    /// nothing of the group is read or written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AC3_ARefusedChangeReadsTheSameWhetherOrNotTheGroupExistsAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration, Permissions.GroupManage);
        Group tellers = await HeldAsync(Branch, "Tellers");
        var absent = new GroupId(Guid.NewGuid());
        int found = _deployment.Groups.Found;

        Answer[] present =
        [
            await RemovedAsync(administrator, tellers.Id),
            await AddedAsync(administrator, tellers.Id, User),
            await MemberRemovedAsync(administrator, tellers.Id, User),
        ];

        Answer[] missing =
        [
            await RemovedAsync(administrator, absent),
            await AddedAsync(administrator, absent, User),
            await MemberRemovedAsync(administrator, absent, User),
        ];

        Assert.Equal(found, _deployment.Groups.Found);
        Assert.All(present.Concat(missing), Denied);
        Assert.Equal(present.Select(Shape), missing.Select(Shape));
        Assert.Empty(_deployment.GroupChanges.Changes);
        Assert.Empty(await _deployment.Groups.MembersAsync(tellers.Id, CancellationToken.None));
        Assert.Single(await _deployment.Groups.InAsync(Branch, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3 and AUTHZ-SCOPE-001: a group the deployment holds no row for
    /// belongs to no organization, so no one holds <c>group:manage</c> where it is, and
    /// a caller holding it in an organization is refused as one holding it nowhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AC3_AGroupNoRowNamesIsRefusedToEveryCallerAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        var absent = new GroupId(Guid.NewGuid());

        Answer removed = await RemovedAsync(administrator, absent);
        Answer added = await AddedAsync(administrator, absent, User);
        Answer taken = await MemberRemovedAsync(administrator, absent, User);

        Assert.All((Answer[])[removed, added, taken], Denied);
        Assert.Empty(_deployment.GroupChanges.Changes);
    }

    /// <summary>
    /// AUTHZ-GROUP-001 and AUTHZ-GRANT-003 AC3: a group that holds a member, belongs to
    /// a group, or was ever given a grant is not removed, and one nothing names is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_OnlyAGroupNothingNamesIsRemovedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId department = Id(await CreatedAsync(administrator, "Department"));
        GroupId team = Id(await CreatedAsync(administrator, "Team"));
        GroupId granted = Id(await CreatedAsync(administrator, "Readers"));
        GroupId empty = Id(await CreatedAsync(administrator, "Empty"));

        _ = await AddedAsync(administrator, department, GrantSubject.Of(team));
        await GivenAsync(granted, Reader, Branch);

        Answer holding = await RemovedAsync(administrator, department);
        Answer held = await RemovedAsync(administrator, team);
        Answer named = await RemovedAsync(administrator, granted);
        Answer removed = await RemovedAsync(administrator, empty);

        foreach (Answer refused in (Answer[])[holding, held, named])
        {
            Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
            Assert.Equal(ErrorCodes.GroupInUse.ToString(), refused.Text("code"));
        }

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Null(await _deployment.Groups.FindAsync(empty, CancellationToken.None));
        Assert.Equal(3, (await _deployment.Groups.InAsync(Branch, CancellationToken.None)).Count);

        GroupAuditInMemory.GroupChange change = _deployment.GroupChanges.Changes[^1];

        Assert.Equal(AuditActions.GroupRemoved, change.Action);
        Assert.Equal(empty, change.Group);
        Assert.Equal("No longer used.", change.Reason);
        Assert.Equal(actor, change.Actor);
    }

    /// <summary>
    /// OPS-CFG-007 AC1: a member of a group holds what it and every group holding it
    /// hold, so a change of members where one of them holds system administration is
    /// made only by a system administrator.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_007_AC1_ChangingAnAdministeringGroupNeedsSystemAdministrationAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Administration, Permissions.GroupManage);
        GroupId operators = Id(await CreatedAsync(administrator, "Operators", Administration));
        GroupId night = Id(await CreatedAsync(administrator, "Night shift", Administration));

        _ = await AddedAsync(administrator, operators, GrantSubject.Of(night));
        await GivenAsync(operators, SystemAdministrator, Administration);

        Answer direct = await AddedAsync(administrator, operators, User);
        Answer nested = await AddedAsync(administrator, night, User);

        _deployment.Gate.Grant(actor, Administration, Permissions.SystemAdminister);

        Answer administered = await AddedAsync(administrator, night, User);

        _deployment.Gate.Revoke(actor, Administration, Permissions.SystemAdminister);

        Answer taken = await MemberRemovedAsync(administrator, night, User);

        foreach (Answer refused in (Answer[])[direct, nested, taken])
        {
            Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
            Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        }

        Assert.Equal(StatusCodes.Status204NoContent, administered.Status);
        Assert.Equal([User], await _deployment.Groups.MembersAsync(night, CancellationToken.None));
    }

    /// <summary>
    /// AUTH-STEP-001: a change of members is the <c>grant:manage</c> step-up action, so
    /// a session whose proof is no longer recent changes nothing; creating a group,
    /// which confers nothing, is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_AChangeOfMembersIsAStepUpActionAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId tellers = Id(await CreatedAsync(administrator, "Tellers"));

        _ = await AddedAsync(administrator, tellers, User);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer added = await AddedAsync(administrator, tellers, GrantSubject.Of(new SubjectId(Guid.NewGuid())));
        Answer removed = await MemberRemovedAsync(administrator, tellers, User);
        Answer created = await CreatedAsync(administrator, "Auditors");

        Assert.Equal(StatusCodes.Status403Forbidden, added.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), added.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, removed.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), removed.Text("code"));
        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.Equal([User], await _deployment.Groups.MembersAsync(tellers, CancellationToken.None));
    }

    /// <summary>
    /// AUTHZ-GROUP-001: adding a member already held, or taking out one that is not,
    /// changes nothing and records nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AChangeThatChangesNothingRecordsNothingAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId tellers = Id(await CreatedAsync(administrator, "Tellers"));

        _ = await AddedAsync(administrator, tellers, User);

        Answer again = await AddedAsync(administrator, tellers, User);
        Answer absent = await MemberRemovedAsync(
            administrator,
            tellers,
            GrantSubject.Of(new SubjectId(Guid.NewGuid())));

        Assert.Equal(StatusCodes.Status204NoContent, again.Status);
        Assert.Equal(StatusCodes.Status204NoContent, absent.Status);
        Assert.Equal(2, _deployment.GroupChanges.Changes.Count);
        Assert.Equal([User], await _deployment.Groups.MembersAsync(tellers, CancellationToken.None));
    }

    /// <summary>
    /// OPS-BOOT-002: the reserved account the break-glass session belongs to cannot be
    /// granted anything further, a group's grants included, so it is made a member of
    /// no group and the refusal records nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_002_TheReservedAccountJoinsNoGroupAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId tellers = Id(await CreatedAsync(administrator, "Tellers"));
        int recorded = _deployment.GroupChanges.Changes.Count;

        _deployment.Reserves(new SubjectId(Holder));

        Answer refused = await AddedAsync(administrator, tellers, User);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        Assert.Empty(await _deployment.Groups.MembersAsync(tellers, CancellationToken.None));
        Assert.Equal(recorded, _deployment.GroupChanges.Changes.Count);
    }

    /// <summary>
    /// AUTHZ-GROUP-001 and API-CONV-002: a change names an organization, a name and a
    /// reason of 1 to 1024 characters, and a member group of the same organization;
    /// anything else is a malformed request naming the field. A group the deployment
    /// holds no row for is refused rather than malformed (CONV-DESIGN-002 AC3).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_WhatAChangeNamesMustBeReadableAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId tellers = Id(await CreatedAsync(administrator, "Tellers"));
        Group foreign = await HeldAsync(Administration, "Operators");

        Answer unscoped = await administrator.SendAsync("GET", "/admin/groups");
        Answer unnamed = await CreatedAsync(administrator, " ");
        Answer overlong = await CreatedAsync(administrator, new string('n', 1025));
        Answer unreasoned = await CreatedAsync(administrator, "Auditors", reason: " ");
        Answer crossing = await AddedAsync(administrator, tellers, GrantSubject.Of(foreign.Id));
        Answer untyped = await administrator.SendAsync(
            "POST",
            "/admin/groups/" + tellers + "/members",
            ("subjectId", Holder),
            ("reason", "Needs it."));
        Answer silent = await AddedAsync(administrator, tellers, User, reason: string.Empty);

        Assert.Equal("organization", Member(unscoped));
        Assert.Equal("name", Member(unnamed));
        Assert.Equal("name", Member(overlong));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("subjectId", Member(crossing));
        Assert.Equal("subjectType", Member(untyped));
        Assert.Equal("reason", Member(silent));
        Assert.Empty(await _deployment.Groups.MembersAsync(tellers, CancellationToken.None));
    }

    private static GrantSubject User => GrantSubject.Of(new SubjectId(Holder));

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private static void Denied(Answer answer)
    {
        Assert.Equal(StatusCodes.Status403Forbidden, answer.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), answer.Text("code"));
    }

    // The members a refusal's body carries, its details among them, which is all of it
    // a caller could tell two refusals apart by beside the values that are new each time.
    private static string Shape(Answer answer)
    {
        JsonElement body = answer.Json();

        return string.Join(
            ',',
            body.EnumerateObject()
                .Select(member => member.Name)
                .Concat(body.TryGetProperty("details", out JsonElement details) && details.ValueKind is JsonValueKind.Object
                    ? details.EnumerateObject().Select(member => "details." + member.Name)
                    : [])
                .Order(StringComparer.Ordinal));
    }

    private static GroupId Id(Answer created)
    {
        Assert.Equal(StatusCodes.Status201Created, created.Status);

        return new GroupId(Guid.Parse(created.Text("id")));
    }

    private static Task<Answer> CreatedAsync(
        Browser administrator,
        string name,
        OrganizationId? organization = null,
        string reason = "Needs it.") =>
        administrator.SendAsync(
            "POST",
            "/admin/groups",
            ("organization", (organization ?? Branch).Value),
            ("name", name),
            ("reason", reason));

    private static Task<Answer> RemovedAsync(Browser administrator, GroupId group) =>
        administrator.SendAsync("DELETE", "/admin/groups/" + group, ("reason", "No longer used."));

    private static Task<Answer> AddedAsync(
        Browser administrator,
        GroupId group,
        GrantSubject member,
        string reason = "Needs it.") =>
        administrator.SendAsync(
            "POST",
            "/admin/groups/" + group + "/members",
            ("subjectType", member.Type is SubjectType.User ? "user" : "group"),
            ("subjectId", member.Value),
            ("reason", reason));

    private static Task<Answer> MemberRemovedAsync(Browser administrator, GroupId group, GrantSubject member) =>
        administrator.SendAsync(
            "DELETE",
            "/admin/groups/" + group + "/members",
            ("subjectType", member.Type is SubjectType.User ? "user" : "group"),
            ("subjectId", member.Value),
            ("reason", "Moved on."));

    private async Task<Group> HeldAsync(OrganizationId organization, string name)
    {
        var group = Group.Create(new GroupId(Guid.NewGuid()), organization, name);

        await _deployment.Groups.CreateAsync(group, CancellationToken.None);

        return group;
    }

    // An organization-wide grant to the group, written as an administrator would.
    private async Task GivenAsync(GroupId group, RoleName role, OrganizationId organization)
    {
        Grant given = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(group),
                role,
                organization,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                new SubjectId(Holder),
                _deployment.Clock.GetUtcNow(),
                "Given for the test.")
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(given, CancellationToken.None);
    }

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(
        OrganizationId organization,
        params Permission[] permissions)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        foreach (Permission permission in permissions)
        {
            _deployment.Gate.Grant(subject, organization, permission);
        }

        return (browser, subject);
    }
}
