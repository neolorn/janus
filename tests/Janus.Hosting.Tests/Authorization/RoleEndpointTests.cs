using System;
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
/// Runtime role management over <c>/admin/roles</c> of chapter 09 section 8, behind
/// <c>role:manage</c> in the administrative organization and the <c>grant:manage</c>
/// step-up (AUTHZ-GRANT-004, OPS-CFG-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class RoleEndpointTests : IAsyncLifetime
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName Editor = RoleName.Parse("editor");

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    private static readonly string[] Editing = ["article:read", "article:edit"];

    private static readonly string[] Reading = ["article:read"];

    private static readonly string[] Administering = ["article:read", "system:administer"];

    private static readonly string[] Undeclared = ["ledger:close"];

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public RoleEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <summary>
    /// The seeded administrative role.
    /// </summary>
    /// <returns>The work of writing it.</returns>
    public async ValueTask InitializeAsync() =>
        await _deployment.Roles.CreateAsync(Role.Of(SystemAdministrator, Permissions.All), CancellationToken.None);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GRANT-004 AC1: a role is created with its permissions at runtime, reads
    /// back at once, and the change is written down with who, what and why.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC1_ARoleIsCreatedWithItsPermissionsWithoutARestartAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        Answer created = await DefinedAsync(administrator, Editing);
        Answer read = await administrator.SendAsync("GET", "/admin/roles");

        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement editor = read.Json()
            .EnumerateArray()
            .Single(role => role.GetProperty("name").GetString() == "editor");

        Assert.Equal(
            ["article:edit", "article:read"],
            editor.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()));

        RoleAuditInMemory.RoleChange change = Assert.Single(_deployment.RoleChanges.Changes);

        Assert.Equal(AuditActions.RoleDefined, change.Action);
        Assert.Equal(Editor, change.Role);
        Assert.Null(change.Before);
        Assert.Equal(2, change.After!.Count);
        Assert.Equal("Needs it.", change.Reason);
        Assert.Equal(actor, change.Actor);
    }

    /// <summary>
    /// AUTHZ-GRANT-004: a role that stands is changed in place, answered with nothing,
    /// and the change records what it was and what it became.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_ARoleIsChangedInPlaceAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing);
        Answer changed = await DefinedAsync(administrator, Reading);

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);

        Role held = (await _deployment.Roles.FindAsync(Editor, CancellationToken.None))!;

        Assert.True(held.Allows(Permission.Parse("article:read")));
        Assert.False(held.Allows(Permission.Parse("article:edit")));

        RoleAuditInMemory.RoleChange change = _deployment.RoleChanges.Changes[^1];

        Assert.Equal(2, change.Before!.Count);
        Assert.Equal([Permission.Parse("article:read")], change.After);
    }

    /// <summary>
    /// AUTHZ-MODEL-004: a role permits only what the model declares, so a permission
    /// neither the library nor the host declared is refused, as is a reason that is
    /// blank.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_004_ARoleNamesOnlyDeclaredPermissionsAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        Answer undeclared = await DefinedAsync(administrator, Undeclared);
        Answer unreasoned = await DefinedAsync(administrator, Reading, reason: " ");

        Assert.Equal("permissions", Member(undeclared));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: every route asks <c>role:manage</c>, in the administrative
    /// organization, and answers forbidden without it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_RoleManageIsAskedInTheAdministrativeOrganizationAsync()
    {
        Browser elsewhere = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Branch, Permissions.RoleManage);

        Answer read = await elsewhere.SendAsync("GET", "/admin/roles");
        Answer defined = await DefinedAsync(elsewhere, Reading);
        Answer removed = await RemovedAsync(elsewhere, "system-administrator");

        Assert.All(
            [read, defined, removed],
            answer => Assert.Equal(StatusCodes.Status403Forbidden, answer.Status));
        Assert.Equal(ErrorCodes.Denied.ToString(), read.Text("code"));
    }

    /// <summary>
    /// OPS-CFG-007 AC1: a role manager without system administration can neither give a
    /// role the permission nor change a role that carries it, and one holding it can.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_007_AC1_ARoleCarryingSystemAdministrationNeedsItAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        Answer conferred = await DefinedAsync(administrator, Administering);
        Answer narrowed = await DefinedAsync(administrator, Reading, name: "system-administrator");

        _deployment.Gate.Grant(actor, Administration, Permissions.SystemAdminister);

        Answer administered = await DefinedAsync(administrator, Administering);

        Assert.Equal(StatusCodes.Status403Forbidden, conferred.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), conferred.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, narrowed.Status);
        Assert.True((await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None))!
            .Allows(Permissions.SystemAdminister));
        Assert.Equal(StatusCodes.Status201Created, administered.Status);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 and AUTHZ-GRANT-003 AC3: a role no grant or derivation names is
    /// removed and the removal written down; one a grant names, revoked or not, or a
    /// derivation confers, stays.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_OnlyARoleNothingNamesIsRemovedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Reading);
        _ = await DefinedAsync(administrator, Reading, name: "reader");
        _ = await DefinedAsync(administrator, Reading, name: "auditor");
        await RevokedGrantOfAsync(RoleName.Parse("auditor"), actor);

        Answer removed = await RemovedAsync(administrator, "editor");
        Answer derived = await RemovedAsync(administrator, "reader");
        Answer granted = await RemovedAsync(administrator, "auditor");
        Answer unknown = await RemovedAsync(administrator, "no-such-role");

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.Equal(AuditActions.RoleRemoved, _deployment.RoleChanges.Changes[^1].Action);
        Assert.Equal(StatusCodes.Status409Conflict, derived.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), derived.Text("code"));
        Assert.Equal(StatusCodes.Status409Conflict, granted.Status);
        Assert.Equal("name", Member(unknown));
    }

    /// <summary>
    /// AUTH-STEP-001 and chapter 10 section 5a: defining and removing a role are the
    /// <c>grant:manage</c> step-up action, so a session whose proof is no longer recent
    /// changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_DefiningAndRemovingARoleAreStepUpActionsAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer changed = await DefinedAsync(administrator, Reading);
        Answer removed = await RemovedAsync(administrator, "editor");

        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), changed.Text("code"));
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), removed.Text("code"));
        Assert.True((await _deployment.Roles.FindAsync(Editor, CancellationToken.None))!
            .Allows(Permission.Parse("article:edit")));
        Assert.Single(_deployment.RoleChanges.Changes);
    }

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private static Task<Answer> DefinedAsync(
        Browser administrator,
        string[] permissions,
        string name = "editor",
        string reason = "Needs it.") =>
        administrator.SendAsync(
            "POST",
            "/admin/roles",
            ("name", name),
            ("permissions", permissions),
            ("reason", reason));

    private static Task<Answer> RemovedAsync(Browser administrator, string name) =>
        administrator.SendAsync("DELETE", "/admin/roles/" + name, ("reason", "No longer used."));

    // A grant of the role that has since been revoked, whose row still names it.
    private async Task RevokedGrantOfAsync(RoleName role, SubjectId actor)
    {
        DateTimeOffset now = _deployment.Clock.GetUtcNow();
        Grant grant = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(actor),
                role,
                Branch,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                actor,
                now,
                "Audits the branch.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(grant, CancellationToken.None);
        _ = grant.Revoke(actor, now, "Audit done.");
        await _deployment.AccessGrants.RecordAsync(grant, CancellationToken.None);
    }

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(params Permission[] permissions)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        foreach (Permission permission in permissions)
        {
            _deployment.Gate.Grant(subject, Administration, permission);
        }

        return (browser, subject);
    }
}
