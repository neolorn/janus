using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Roles;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A role's inclusions of other roles over <c>/admin/roles/{name}/inclusions/{included}</c>
/// of chapter 09 section 8, behind <c>roleinclusion:manage</c> in the administrative
/// organization and the <c>grant:manage</c> step-up, and the read of the roles with what
/// each carries through them (AUTHZ-GRANT-006, D-278).
/// </summary>
[Trait("kind", "unit")]
public sealed class RoleInclusionEndpointTests : IAsyncLifetime
{
    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly Permission Read = Permission.Parse("article:read");

    private static readonly Permission Edit = Permission.Parse("article:edit");

    private static readonly RoleName Manager = RoleName.Parse("manager");

    private static readonly RoleName Editor = RoleName.Parse("editor");

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly RoleName Contractor = RoleName.Parse("contractor");

    private static readonly string[] Reading = ["article:read"];

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public RoleInclusionEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <summary>
    /// The roles the cases include one in another: a manager carrying nothing of its own,
    /// an editor that hands out the reader, a reader, and a contractor whose grants must
    /// end.
    /// </summary>
    /// <returns>The work of writing them.</returns>
    public async ValueTask InitializeAsync()
    {
        await _deployment.Roles.CreateAsync(Role.Of(Manager, []), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(Editor, [Edit], [Reader]), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Read]), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(Contractor, [Read], grantsMustEnd: true), CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GRANT-006 AC7, AC9: an inclusion made is answered as created, set again as
    /// nothing, written down with what it was and became, announced as a change to the
    /// including role, and read back with what the role carries through it and from where.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC7_AnInclusionIsMadeSetAndReadBackWithItsSourcesAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleInclusionManage);

        Answer made = await IncludedAsync(administrator, "manager", "editor");
        Answer chained = await IncludedAsync(administrator, "editor", "reader");
        Answer set = await IncludedAsync(administrator, "manager", "editor", leftOutHandsOut: ["reader"]);
        Answer read = await administrator.SendAsync("GET", "/admin/roles");

        Assert.Equal(StatusCodes.Status201Created, made.Status);
        Assert.Equal(StatusCodes.Status201Created, chained.Status);
        Assert.Equal(StatusCodes.Status204NoContent, set.Status);
        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement manager = read.Json()
            .EnumerateArray()
            .Single(role => role.GetProperty("name").GetString() == "manager");
        JsonElement inclusion = Assert.Single(manager.GetProperty("includes").EnumerateArray());

        Assert.Empty(manager.GetProperty("permissions").EnumerateArray());
        Assert.Equal("editor", inclusion.GetProperty("role").GetString());
        Assert.True(inclusion.GetProperty("enabled").GetBoolean());
        Assert.Equal(["reader"], inclusion.GetProperty("leftOut").GetProperty("handsOut").EnumerateArray().Select(Text));
        Assert.Equal(
            [("article:edit", "editor"), ("article:read", "editor/reader")],
            manager.GetProperty("effective").GetProperty("permissions").EnumerateArray()
                .Select(entry => (Text(entry.GetProperty("name")), string.Join('/', entry.GetProperty("from").EnumerateArray().Select(Text)))));
        Assert.Empty(manager.GetProperty("effective").GetProperty("handsOut").EnumerateArray());

        RoleAuditInMemory.InclusionChange change = _deployment.RoleChanges.Inclusions[^1];

        Assert.Equal(AuditActions.RoleIncluded, change.Action);
        Assert.Equal((Manager, Editor), (change.Role, change.Included));
        Assert.Empty(change.Before!.LeftOutHandsOut);
        Assert.Equal([Reader], change.After!.LeftOutHandsOut);
        Assert.Equal(actor, change.Actor);
        Assert.Equal(
            [RoleChange.Changed, RoleChange.Changed, RoleChange.Changed],
            _deployment.Events.Of<RoleChanged>().Select(one => one.Change));
        Assert.Equal("manager", _deployment.Events.Of<RoleChanged>()[^1].Role);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC7: an inclusion removed is answered with nothing and written down
    /// with nothing after it; one that does not exist, or a role the deployment does not
    /// hold, is not found; a role an inclusion names on either side is not removed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC7_AnInclusionIsRemovedAndARoleItNamesStaysAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleInclusionManage, Permissions.RoleManage);

        _ = await IncludedAsync(administrator, "manager", "editor");

        Answer included = await administrator.SendAsync("DELETE", "/admin/roles/editor", ("reason", "No longer used."));
        Answer including = await administrator.SendAsync("DELETE", "/admin/roles/manager", ("reason", "No longer used."));
        Answer removed = await RemovedAsync(administrator, "manager", "editor");
        Answer again = await RemovedAsync(administrator, "manager", "editor");
        Answer unheld = await IncludedAsync(administrator, "manager", "accountant");

        Assert.Equal(StatusCodes.Status409Conflict, included.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), included.Text("code"));
        Assert.Equal(StatusCodes.Status409Conflict, including.Status);
        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Empty(_deployment.Roles.Inclusions.Held);
        Assert.Equal(AuditActions.RoleInclusionRemoved, _deployment.RoleChanges.Inclusions[^1].Action);
        Assert.Null(_deployment.RoleChanges.Inclusions[^1].After);
        Assert.Equal(StatusCodes.Status404NotFound, again.Status);
        Assert.Equal(ErrorCodes.RoleNotFound.ToString(), again.Text("code"));
        Assert.Equal(StatusCodes.Status404NotFound, unheld.Status);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC6: an inclusion that would make a role include itself, at any
    /// depth, is refused <c>authz.role.inclusionloop</c>; a role whose grants must end
    /// included by one whose grants need not end, and turning on <c>grantsMustEnd</c> for
    /// a role an open-ended one includes, are refused <c>authz.role.inclusionmustend</c>;
    /// each writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC6_LoopsAndMustEndBreachesAreRefusedAndWriteNothingAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleInclusionManage, Permissions.RoleManage);

        _ = await IncludedAsync(administrator, "manager", "editor");
        _ = await IncludedAsync(administrator, "editor", "reader");

        int written = _deployment.RoleChanges.Inclusions.Count;

        Answer itself = await IncludedAsync(administrator, "manager", "manager");
        Answer deep = await IncludedAsync(administrator, "reader", "manager");
        Answer ending = await IncludedAsync(administrator, "manager", "contractor");
        Answer turned = await administrator.SendAsync(
            "POST",
            "/admin/roles",
            ("name", "reader"),
            ("permissions", Reading),
            ("grantsMustEnd", true),
            ("reason", "Contractors only."));

        Assert.All([itself, deep], answer => Assert.Equal(StatusCodes.Status409Conflict, answer.Status));
        Assert.All([itself, deep], answer => Assert.Equal(ErrorCodes.RoleInclusionLoop.ToString(), answer.Text("code")));
        Assert.All([ending, turned], answer => Assert.Equal(StatusCodes.Status409Conflict, answer.Status));
        Assert.All([ending, turned], answer => Assert.Equal(ErrorCodes.RoleInclusionMustEnd.ToString(), answer.Text("code")));
        Assert.Equal(2, _deployment.Roles.Inclusions.Held.Count);
        Assert.Equal(written, _deployment.RoleChanges.Inclusions.Count);
        Assert.False((await _deployment.Roles.FindAsync(Reader, CancellationToken.None))!.GrantsMustEnd);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC7: a change without <c>roleinclusion:manage</c> in the
    /// administrative organization, without a reason, leaving out a permission the model
    /// does not declare or a member that is no role name, or beyond the caller's reach is
    /// refused and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC7_ARefusedChangeWritesNothingAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(reaching: false, Permissions.RoleManage);
        Answer denied = await IncludedAsync(administrator, "manager", "editor");

        _deployment.Gate.Grant(actor, Administration, Permissions.RoleInclusionManage);

        Answer unreasoned = await IncludedAsync(administrator, "manager", "editor", reason: " ");
        Answer undeclared = await IncludedAsync(administrator, "manager", "editor", leftOutPermissions: ["ledger:close"]);
        Answer unnamed = await IncludedAsync(administrator, "manager", "editor", leftOutHandsOut: ["Reader"]);
        Answer beyond = await IncludedAsync(administrator, "manager", "editor");

        Assert.Equal(StatusCodes.Status403Forbidden, denied.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), denied.Text("code"));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("leftOut", Member(undeclared));
        Assert.Equal("leftOut", Member(unnamed));
        Assert.Equal(StatusCodes.Status403Forbidden, beyond.Status);
        Assert.Equal(ErrorCodes.RoleNotAssignable.ToString(), beyond.Text("code"));
        Assert.Empty(_deployment.Roles.Inclusions.Held);
        Assert.Empty(_deployment.RoleChanges.Inclusions);
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC9 (D-278): the read of the roles is open to
    /// <c>roleinclusion:manage</c> as it is to <c>role:manage</c>, and refused without
    /// either.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC9_TheRolesAreReadUnderEitherPermissionAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(reaching: false, Permissions.RoleInclusionManage);

        Answer byInclusion = await administrator.SendAsync("GET", "/admin/roles");

        _deployment.Gate.Revoke(actor, Administration, Permissions.RoleInclusionManage);
        _deployment.Gate.Grant(actor, Administration, Permissions.RoleManage);

        Answer byRoles = await administrator.SendAsync("GET", "/admin/roles");

        _deployment.Gate.Revoke(actor, Administration, Permissions.RoleManage);

        Answer refused = await administrator.SendAsync("GET", "/admin/roles");

        Assert.Equal(StatusCodes.Status200OK, byInclusion.Status);
        Assert.Equal(StatusCodes.Status200OK, byRoles.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// AUTHZ-GRANT-006, chapter 09 <c>GET /account/roles</c> (D-280): a person with no
    /// permission reads the roles their own account holds through its live allow grants in
    /// the organization, each with what it carries through its inclusions and its sources,
    /// and the roles whose effective <c>handsOut</c> names it, through inclusions too; a
    /// deny and a grant in another organization are not listed, and an absent organization
    /// is malformed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_APersonReadsTheirOwnRolesWithWhatTheyCarryAsync()
    {
        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId own = _deployment.Directory.Created[^1].Subject;
        var chief = RoleName.Parse("chief");
        var head = RoleName.Parse("head");

        await _deployment.Roles.CreateAsync(Role.Of(chief, [], [Manager]), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(head, []), CancellationToken.None);
        await _deployment.Roles.Inclusions.CreateAsync(Manager, new RoleInclusion(Editor, Enabled: true, [], []), CancellationToken.None);
        await _deployment.Roles.Inclusions.CreateAsync(head, new RoleInclusion(chief, Enabled: true, [], []), CancellationToken.None);
        await HeldAsync(own, Manager, Branch, deny: false);
        await HeldAsync(own, Reader, Branch, deny: true);
        await HeldAsync(own, Editor, Administration, deny: false);

        Answer read = await person.SendAsync("GET", "/account/roles?organization=" + Branch);
        Answer unnamed = await person.SendAsync("GET", "/account/roles");

        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement held = Assert.Single(read.Json().EnumerateArray());
        JsonElement effective = held.GetProperty("effective");

        Assert.Equal("manager", held.GetProperty("name").GetString());
        Assert.Equal(
            [("article:edit", "editor")],
            effective.GetProperty("permissions").EnumerateArray()
                .Select(entry => (Text(entry.GetProperty("name")), string.Join('/', entry.GetProperty("from").EnumerateArray().Select(Text)))));
        Assert.Equal(
            [("reader", "editor")],
            effective.GetProperty("handsOut").EnumerateArray()
                .Select(entry => (Text(entry.GetProperty("name")), string.Join('/', entry.GetProperty("from").EnumerateArray().Select(Text)))));
        Assert.Equal(["chief", "head"], held.GetProperty("givenBy").EnumerateArray().Select(Text));
        Assert.Equal("organization", Member(unnamed));
    }

    private static string? Text(JsonElement element) => element.GetString();

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    // An inclusion whose body names what it leaves out only where it is given.
    private static Task<Answer> IncludedAsync(
        Browser administrator,
        string role,
        string included,
        string reason = "Managers do what editors do.",
        string[]? leftOutPermissions = null,
        string[]? leftOutHandsOut = null) =>
        administrator.SendAsync(
            "PUT",
            "/admin/roles/" + role + "/inclusions/" + included,
            [
                .. leftOutPermissions is null && leftOutHandsOut is null
                    ? []
                    : new (string, object?)[]
                    {
                        ("leftOut", new { permissions = leftOutPermissions ?? [], handsOut = leftOutHandsOut ?? [] }),
                    },
                ("reason", reason),
            ]);

    private static Task<Answer> RemovedAsync(Browser administrator, string role, string included) =>
        administrator.SendAsync(
            "DELETE",
            "/admin/roles/" + role + "/inclusions/" + included,
            ("reason", "No longer needed."));

    // A grant of the role to the account in the organization, standing.
    private async Task HeldAsync(SubjectId holder, RoleName role, OrganizationId organization, bool deny)
    {
        Grant grant = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(holder),
                role,
                organization,
                on: null,
                deny,
                GrantKind.Stored,
                expiresAt: null,
                holder,
                _deployment.Clock.GetUtcNow(),
                "Works in the branch.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(grant, CancellationToken.None);
    }

    // An administrator holding the permissions in the administrative organization and,
    // unless a case leaves it short, reaching what the cases' roles carry and hand out.
    private Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(params Permission[] permissions) =>
        AuthorisedAsync(reaching: true, permissions);

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(bool reaching, params Permission[] permissions)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        foreach (Permission permission in permissions)
        {
            _deployment.Gate.Grant(subject, Administration, permission);
        }

        // AUTHZ-GRANT-005 (D-278): the reach a change is judged on is what it adds to the
        // including role or takes from it.
        if (reaching)
        {
            foreach (Permission reached in (Permission[])[Read, Edit])
            {
                _deployment.Gate.Grant(subject, Administration, reached);
            }

            _deployment.HandOuts.HandsOut(subject, Administration, Reader);
        }

        return (browser, subject);
    }
}
