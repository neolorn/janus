using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Roles;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.Equal(2, change.After!.Permissions.Count);
        Assert.Empty(change.After.HandsOut);
        Assert.Equal("Needs it.", change.Reason);
        Assert.Equal(actor, change.Actor);
    }

    /// <summary>
    /// AUTHZ-GRANT-004: a role that stands is changed in place, answered with nothing,
    /// and the change records what it was and what it became.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC4_ARoleIsChangedInPlaceAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing);
        Answer changed = await DefinedAsync(administrator, Reading);

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);

        Role held = (await _deployment.Roles.FindAsync(Editor, CancellationToken.None))!;

        Assert.True(held.Allows(Permission.Parse("article:read")));
        Assert.False(held.Allows(Permission.Parse("article:edit")));

        RoleAuditInMemory.RoleChange change = _deployment.RoleChanges.Changes[^1];

        Assert.Equal(2, change.Before!.Permissions.Count);
        Assert.Equal([Permission.Parse("article:read")], change.After!.Permissions);
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
    /// OPS-CFG-007 AC3: a role manager without system administration can neither give a
    /// role the permission nor change a role that carries it, and one holding it can.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_007_AC3_ARoleCarryingSystemAdministrationNeedsItAsync()
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

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.Equal(AuditActions.RoleRemoved, _deployment.RoleChanges.Changes[^1].Action);
        Assert.Equal(StatusCodes.Status409Conflict, derived.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), derived.Text("code"));
        Assert.Equal(StatusCodes.Status409Conflict, granted.Status);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC6 and REG-INV-001: a role an open invitation names grants at
    /// the acknowledgement, so it is not removed while the invitation stands, and is
    /// removed once the invitation is revoked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC6_ARoleAnOpenInvitationNamesIsNotRemovedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Reading);

        Invitation invitation = await InvitedAsync(actor, Editor, _deployment.Clock.GetUtcNow());

        Answer refused = await RemovedAsync(administrator, "editor");

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), refused.Text("code"));
        Assert.NotNull(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));

        invitation.Revoke(_deployment.Clock.GetUtcNow());

        Answer removed = await RemovedAsync(administrator, "editor");

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC6: an invitation stands until it is acknowledged or revoked,
    /// expired or not, so a role an expired one names is not removed either.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC6_ARoleAStandingInvitationNamesIsNotRemovedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Reading);

        Invitation invitation = await InvitedAsync(actor, Editor, _deployment.Clock.GetUtcNow().AddDays(-8));

        Answer refused = await RemovedAsync(administrator, "editor");

        Assert.True(invitation.HasExpired(_deployment.Clock.GetUtcNow()));
        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), refused.Text("code"));
        Assert.NotNull(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC6: a role a grant names, live, expired or revoked, and a role a
    /// declared derivation confers are each refused removal with
    /// <c>authz.role.inuse</c>, and every one of them stands with no removal written
    /// down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC6_ARoleAGrantOrADerivationNamesIsNotRemovedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);
        string[] named = ["granted", "lapsed", "auditor", "reader"];

        foreach (string name in named)
        {
            _ = await DefinedAsync(administrator, Reading, name: name);
        }

        await GrantOfAsync(RoleName.Parse("granted"), actor, expiresAt: null);
        await GrantOfAsync(RoleName.Parse("lapsed"), actor, _deployment.Clock.GetUtcNow().AddMinutes(1));
        await RevokedGrantOfAsync(RoleName.Parse("auditor"), actor);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        var refusals = new List<Answer>();

        foreach (string name in named)
        {
            refusals.Add(await RemovedAsync(administrator, name));
        }

        Assert.All(
            refusals,
            refused =>
            {
                Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
                Assert.Equal(ErrorCodes.RoleInUse.ToString(), refused.Text("code"));
            });

        foreach (string name in named)
        {
            Assert.NotNull(await _deployment.Roles.FindAsync(RoleName.Parse(name), CancellationToken.None));
        }

        Assert.DoesNotContain(_deployment.RoleChanges.Changes, change => change.Action == AuditActions.RoleRemoved);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC6 (D-212): a role another role's <c>handsOut</c> names is not
    /// removed while that role names it, and is removed once it no longer does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC6_ARoleAnotherRolesHandsOutNamesIsNotRemovedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Reading, name: "proofreader");
        _ = await DefinedAsync(administrator, Editing, handsOut: ["proofreader"]);

        Answer refused = await RemovedAsync(administrator, "proofreader");

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RoleInUse.ToString(), refused.Text("code"));
        Assert.NotNull(await _deployment.Roles.FindAsync(RoleName.Parse("proofreader"), CancellationToken.None));

        Answer emptied = await DefinedAsync(administrator, Editing);

        Assert.Equal(StatusCodes.Status204NoContent, emptied.Status);
        Assert.Equal(StatusCodes.Status204NoContent, (await RemovedAsync(administrator, "proofreader")).Status);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-212): a role is defined with the roles its holders hand out,
    /// which read back with it and are recorded before and after; a body naming none
    /// gives it none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ARoleCarriesTheRolesItsHoldersHandOutAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Reading, name: "reader");
        Answer created = await DefinedAsync(administrator, Editing, handsOut: ["reader", "editor"]);
        Answer read = await administrator.SendAsync("GET", "/admin/roles");

        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.Equal(
            ["editor", "reader"],
            read.Json()
                .EnumerateArray()
                .Single(role => role.GetProperty("name").GetString() == "editor")
                .GetProperty("handsOut")
                .EnumerateArray()
                .Select(handed => handed.GetString()));
        Assert.Empty(
            read.Json()
                .EnumerateArray()
                .Single(role => role.GetProperty("name").GetString() == "reader")
                .GetProperty("handsOut")
                .EnumerateArray());

        Answer changed = await DefinedAsync(administrator, Editing);
        RoleAuditInMemory.RoleChange change = _deployment.RoleChanges.Changes[^1];

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Equal([RoleName.Parse("editor"), RoleName.Parse("reader")], change.Before!.HandsOut);
        Assert.Empty(change.After!.HandsOut);
        Assert.Empty((await _deployment.Roles.FindAsync(Editor, CancellationToken.None))!.HandsOut);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-212): a <c>handsOut</c> naming what is not a role name is
    /// refused as malformed naming it, and one naming a role the deployment does not hold
    /// is refused <c>authz.grant.unresolved</c> naming it; neither defines the role or
    /// records anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AHandsOutOutsideTheRuleOrNamingNoRoleIsRefusedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        Answer malformed = await DefinedAsync(administrator, Reading, handsOut: ["Reader"]);
        Answer unresolved = await DefinedAsync(administrator, Reading, handsOut: ["accountant"]);

        Assert.Equal("handsOut", Member(malformed));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unresolved.Status);
        Assert.Equal(ErrorCodes.GrantUnresolved.ToString(), unresolved.Text("code"));
        Assert.Equal("handsOut", unresolved.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.Empty(_deployment.RoleChanges.Changes);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC3: a definition, a change and a removal whose reason is absent,
    /// or blank after trimming, are refused naming it to a caller who may make the
    /// change, and the roles stand as they were with nothing more written down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC3_ARoleChangeWithoutAReasonChangesNothingAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing, name: "writer");

        int recorded = _deployment.RoleChanges.Changes.Count;

        Answer[] refusals =
        [
            await administrator.SendAsync("POST", "/admin/roles", ("name", "editor"), ("permissions", Editing)),
            await DefinedAsync(administrator, Editing, reason: " "),
            await administrator.SendAsync("POST", "/admin/roles", ("name", "writer"), ("permissions", Reading)),
            await DefinedAsync(administrator, Reading, name: "writer", reason: " "),
            await administrator.SendAsync("DELETE", "/admin/roles/writer", "{}"),
            await administrator.SendAsync("DELETE", "/admin/roles/writer", ("reason", " ")),
        ];

        Assert.All(refusals, refused => Assert.Equal("reason", Member(refused)));
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.True((await _deployment.Roles.FindAsync(RoleName.Parse("writer"), CancellationToken.None))!
            .Allows(Permission.Parse("article:edit")));
        Assert.Equal(recorded, _deployment.RoleChanges.Changes.Count);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC5: a change taking a library-owned permission out of the role
    /// the reserved account holds, and a revocation of that account's grant of the
    /// role, are each refused with <c>authz.denied</c> to a stepped-up system
    /// administrator, and the role, the grant and the record of changes stand as they
    /// were.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC5_TheReservedAccountsRoleAndGrantAreNotTakenAwayAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(
            Permissions.RoleManage,
            Permissions.GrantManage,
            Permissions.SystemAdminister);
        var reserved = new SubjectId(Guid.NewGuid());
        Grant bootstrapped = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(reserved),
                SystemAdministrator,
                Administration,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                actor,
                _deployment.Clock.GetUtcNow(),
                "Bootstrap.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(bootstrapped, CancellationToken.None);
        _deployment.Reserves(reserved);

        Answer narrowed = await DefinedAsync(
            administrator,
            [.. Permissions.All.Skip(1).Select(permission => permission.ToString())],
            name: SystemAdministrator.ToString());
        Answer revoked = await administrator.SendAsync(
            "DELETE",
            "/admin/grants/" + bootstrapped.Id,
            ("reason", "No longer needed."));

        Assert.All(
            [narrowed, revoked],
            refused =>
            {
                Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
                Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
            });

        Role standing = (await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None))!;

        Assert.All(Permissions.All, permission => Assert.True(standing.Allows(permission)));
        Assert.Null((await _deployment.AccessGrants.FindAsync(bootstrapped.Id, CancellationToken.None))!.RevokedAt);
        Assert.Empty(_deployment.RoleChanges.Changes);
    }

    /// <summary>
    /// AUTHZ-GRANT-004, D-166: a path naming a role the deployment does not hold is a
    /// record not found, answered after the permission and the reason are judged, and
    /// nothing is written down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AnUnknownRoleIsNotFoundAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync();

        Answer refused = await RemovedAsync(administrator, "no-such-role");

        _deployment.Gate.Grant(subject, Administration, Permissions.RoleManage);

        Answer unreasoned = await administrator.SendAsync("DELETE", "/admin/roles/no-such-role", "{}");
        Answer unknown = await RemovedAsync(administrator, "no-such-role");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal(StatusCodes.Status404NotFound, unknown.Status);
        Assert.Equal(ErrorCodes.RoleNotFound.ToString(), unknown.Text("code"));
        Assert.Empty(_deployment.RoleChanges.Changes);
    }

    /// <summary>
    /// OPS-BOOT-002 AC7, D-166: the role the reserved account holds keeps every permission
    /// the library declares, so a stepped-up system administrator taking one out is
    /// refused and nothing changes; adding a permission the host declares stays
    /// allowed, and a role the reserved account does not hold is narrowed as before.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_002_AC7_TheReservedAccountsRoleKeepsEveryLibraryPermissionAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(
            Permissions.RoleManage,
            Permissions.SystemAdminister);
        var reserved = new SubjectId(Guid.NewGuid());
        Grant bootstrapped = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(reserved),
                SystemAdministrator,
                Administration,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                actor,
                _deployment.Clock.GetUtcNow(),
                "Bootstrap.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(bootstrapped, CancellationToken.None);
        _deployment.Reserves(reserved);

        string[] library = [.. Permissions.All.Select(permission => permission.ToString())];

        Answer narrowed = await DefinedAsync(administrator, [.. library.Skip(1)], name: SystemAdministrator.ToString());

        Assert.Equal(StatusCodes.Status403Forbidden, narrowed.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), narrowed.Text("code"));
        Role standing = (await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None))!;

        Assert.All(Permissions.All, permission => Assert.True(standing.Allows(permission)));
        Assert.Empty(_deployment.RoleChanges.Changes);

        Answer widened = await DefinedAsync(administrator, [.. library, "article:read"], name: SystemAdministrator.ToString());
        Answer other = await DefinedAsync(administrator, Administering, name: "auditor");
        Answer otherNarrowed = await DefinedAsync(administrator, Reading, name: "auditor");

        Assert.Equal(StatusCodes.Status204NoContent, widened.Status);
        Assert.True((await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None))!
            .Allows(Permission.Parse("article:read")));
        Assert.Equal(StatusCodes.Status201Created, other.Status);
        Assert.Equal(StatusCodes.Status204NoContent, otherNarrowed.Status);
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

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a definition and a removal refused on the role as it
    /// stands, or for want of the step-up, are refused before their unit of work begins,
    /// so none is opened for them and none is left open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_ADefinitionOrARemovalRefusedBeforeItsStepUpBeginsNoUnitOfWorkAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing);
        _ = await DefinedAsync(administrator, Reading, name: "reader");

        int rolledBack = _deployment.Work.RolledBack;

        Answer conferred = await DefinedAsync(administrator, Administering);

        Assert.Equal(ErrorCodes.Denied.ToString(), conferred.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());

        Answer narrowed = await RemovedAsync(administrator, "system-administrator");

        Assert.Equal(ErrorCodes.Denied.ToString(), narrowed.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());

        Answer unknown = await RemovedAsync(administrator, "no-such-role");

        Assert.Equal(ErrorCodes.RoleNotFound.ToString(), unknown.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());

        Answer derived = await RemovedAsync(administrator, "reader");

        Assert.Equal(ErrorCodes.RoleInUse.ToString(), derived.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer changed = await DefinedAsync(administrator, Reading);

        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), changed.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());

        Answer removed = await RemovedAsync(administrator, "editor");

        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), removed.Text("code"));
        Assert.Equal((false, 0, rolledBack), Ended());
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a definition and a removal admitted on the role as it stood,
    /// and refused on it as it is once its row is held, because another transaction
    /// changed it meanwhile, each roll their unit of work back, so none is left open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADefinitionOrARemovalRefusedAfterItBeganRollsBackAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Permissions.RoleManage);

        _ = await DefinedAsync(administrator, Editing);
        _ = await DefinedAsync(administrator, Reading, name: "drafter");

        int recorded = _deployment.RoleChanges.Changes.Count;
        int rolledBack = _deployment.Work.RolledBack;

        _deployment.Roles.Locking = name =>
        {
            _deployment.Roles.Locking = null;
            _ = _deployment.Roles.RecordAsync(Role.Of(name, Permissions.All), CancellationToken.None).AsTask();
        };

        Answer conferred = await DefinedAsync(administrator, Reading);

        Assert.Equal(ErrorCodes.Denied.ToString(), conferred.Text("code"));
        Assert.Equal((false, 0, rolledBack + 1), Ended());

        _deployment.Roles.Locking = name =>
        {
            _deployment.Roles.Locking = null;
            _ = _deployment.Roles.RemoveAsync(name, CancellationToken.None).AsTask();
        };

        Answer removed = await RemovedAsync(administrator, "drafter");

        Assert.Equal(ErrorCodes.RoleNotFound.ToString(), removed.Text("code"));
        Assert.Equal((false, 0, rolledBack + 2), Ended());
        Assert.Equal(recorded, _deployment.RoleChanges.Changes.Count);
    }

    /// <summary>
    /// CONV-CODE-006 AC2: a body missing the reason defining or removing a role requires,
    /// or carrying one past 1024 characters (API-CONV-002), is refused naming it before
    /// the service is reached, so a caller the service would refuse for want of the
    /// permission is answered for the body, and nothing changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_ABodyMissingAMemberIsRefusedBeforeTheServiceAsync()
    {
        (Browser caller, _) = await AuthorisedAsync();

        Answer undefined = await caller.SendAsync(
            "POST",
            "/admin/roles",
            ("name", "editor"),
            ("permissions", Editing));
        Answer unremoved = await caller.SendAsync("DELETE", "/admin/roles/" + SystemAdministrator, "{}");
        Answer overlong = await DefinedAsync(caller, Editing, reason: new string('r', 1025));

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), undefined.Text("code"));
        Assert.Equal("reason", Member(undefined));
        Assert.Equal("reason", Member(unremoved));
        Assert.Equal("reason", Member(overlong));
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.NotNull(await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None));
        Assert.Empty(_deployment.RoleChanges.Changes);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction committed after the gate step and before the
    /// first write refuses a role's definition and its removal inside their unit of
    /// work, which rolls back and leaves nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesARoleChangeAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Permissions.RoleManage);

        Assert.Equal(StatusCodes.Status201Created, (await DefinedAsync(administrator, Reading, "drafter")).Status);

        int recorded = _deployment.RoleChanges.Changes.Count;

        foreach (Func<Task<Answer>> change in new Func<Task<Answer>>[]
        {
            () => DefinedAsync(administrator, Editing),
            () => RemovedAsync(administrator, "drafter"),
        })
        {
            int rolledBack = _deployment.Work.RolledBack;

            _deployment.Gate.Admitted = _ => _deployment.Work.Meanwhile = () => _deployment.Gate.Restrict(actor);

            Answer refused = await change();

            _deployment.Gate.Lift(actor);

            Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
            Assert.Equal(ErrorCodes.Restricted.ToString(), refused.Text("code"));
            Assert.Equal(rolledBack + 1, _deployment.Work.RolledBack);
            Assert.False(_deployment.Work.Open);
        }

        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.NotNull(await _deployment.Roles.FindAsync(RoleName.Parse("drafter"), CancellationToken.None));
        Assert.Equal(recorded, _deployment.RoleChanges.Changes.Count);
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC7 (D-204): reading, defining and removing a role with a system
    /// principal's context is refused with <c>authz.denied</c>, the start principal
    /// <c>role-declaration</c> included, and changes nothing: a declared role is defined
    /// by the start step alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC7_AnIRolesCallWithASystemPrincipalsContextIsRefusedAsync()
    {
        SystemPrincipal[] principals =
        [
            DeclaredRoleSeed.Principal,
            SystemPrincipal.ForDeployment("bootstrap", "OPS-BOOT-001", SystemOperation.Bootstrap),
            SystemPrincipal.ForOrganization("ledger-close", "LIB-HOST-001", Administration),
        ];
        var session = SessionId.New(_deployment.Clock);
        var refusals = new List<Error?>();

        foreach (SystemPrincipal principal in principals)
        {
            var context = AccessContext.Of(principal);

            await using AsyncServiceScope scope = _deployment.Scope();
            IRoles roles = scope.ServiceProvider.GetRequiredService<IRoles>();

            refusals.Add((await roles.AllAsync(context, CancellationToken.None))
                .Match<Error?>(_ => null, error => error));
            refusals.Add((await roles.DefineAsync(
                    context,
                    session,
                    new DefinedRole(Editor, [Permission.Parse("article:read")], []),
                    "Seeded at the start.",
                    CancellationToken.None))
                .Match<Error?>(_ => null, error => error));
            refusals.Add((await roles.RemoveAsync(context, session, SystemAdministrator, "Not needed.", CancellationToken.None))
                .Match<Error?>(() => null, error => error));
        }

        Assert.Equal(9, refusals.Count);
        Assert.All(refusals, refused => Assert.Equal(ErrorCodes.Denied, refused?.Code));
        Assert.Null(await _deployment.Roles.FindAsync(Editor, CancellationToken.None));
        Assert.NotNull(await _deployment.Roles.FindAsync(SystemAdministrator, CancellationToken.None));
        Assert.Empty(_deployment.RoleChanges.Changes);
        Assert.Empty(_deployment.RoleChanges.Principals);
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
        string reason = "Needs it.",
        string[]? handsOut = null) =>
        handsOut is null
            ? administrator.SendAsync(
                "POST",
                "/admin/roles",
                ("name", name),
                ("permissions", permissions),
                ("reason", reason))
            : administrator.SendAsync(
                "POST",
                "/admin/roles",
                ("name", name),
                ("permissions", permissions),
                ("handsOut", handsOut),
                ("reason", reason));

    private static Task<Answer> RemovedAsync(Browser administrator, string name) =>
        administrator.SendAsync("DELETE", "/admin/roles/" + name, ("reason", "No longer used."));

    // Whether a unit of work is open, how many were left neither committed nor rolled
    // back, and how many were rolled back.
    private (bool Open, int Unended, int RolledBack) Ended() =>
        (
            _deployment.Work.Open,
            _deployment.Work.Opened - _deployment.Work.Committed - _deployment.Work.RolledBack,
            _deployment.Work.RolledBack);

    // An invitation into the branch naming the role, standing, for seven days from
    // when it was issued.
    private async Task<Invitation> InvitedAsync(SubjectId inviter, RoleName role, DateTimeOffset issuedAt)
    {
        var invitation = Invitation.Issued(
            InvitationId.New(_deployment.Clock),
            Branch,
            inviter,
            new InvitedIdentifiers("invited@example.test", null, null),
            [role],
            [],
            mailbox: null,
            OpaqueToken.Of(Guid.NewGuid().ToString("N")).Fingerprint(),
            issuedAt,
            TimeSpan.FromDays(7));

        await _deployment.Invitations.AddAsync(invitation, CancellationToken.None);

        return invitation;
    }

    // A grant of the role in the branch that stands, until it expires where it does.
    private async Task GrantOfAsync(RoleName role, SubjectId actor, DateTimeOffset? expiresAt)
    {
        Grant grant = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(actor),
                role,
                Branch,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt,
                actor,
                _deployment.Clock.GetUtcNow(),
                "Works in the branch.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(grant, CancellationToken.None);
    }

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

        // AUTHZ-GRANT-005 (D-212): an administrator of roles reaches what the roles these
        // cases define carry and hand out.
        if (permissions.Contains(Permissions.RoleManage))
        {
            foreach (string permission in Editing)
            {
                _deployment.Gate.Grant(subject, Administration, Permission.Parse(permission));
            }

            foreach (string role in (string[])["editor", "reader", "proofreader"])
            {
                _deployment.HandOuts.HandsOut(subject, Administration, RoleName.Parse(role));
            }
        }

        return (browser, subject);
    }
}
