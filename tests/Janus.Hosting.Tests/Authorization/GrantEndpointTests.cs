using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Writing and revoking stored grants over <c>/admin/grants</c> of chapter 09 section 8,
/// behind <c>grant:manage</c> in the grant's organization and its step-up
/// (AUTHZ-GRANT-001 to AUTHZ-GRANT-003, OPS-CFG-007), and reading what one holder
/// holds behind <c>grant:read</c> there (entry 268).
/// </summary>
[Trait("kind", "unit")]
public sealed class GrantEndpointTests : IAsyncLifetime
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    private static readonly ResourceReference Document =
        new(ResourceType.Parse("document"), ResourceId.Parse("d-1"));

    private static readonly Guid Holder = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public GrantEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <summary>
    /// A reading role, the seeded administrative role and one registered record.
    /// </summary>
    /// <returns>The work of writing them.</returns>
    public async ValueTask InitializeAsync()
    {
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Permission.Parse("document:read")]), CancellationToken.None);
        await _deployment.Roles.CreateAsync(Role.Of(SystemAdministrator, Permissions.All), CancellationToken.None);
        await _deployment.Resources.RegisterAsync(
            RegisteredResource.Create(Document, Branch, subject: null, containedIn: null),
            CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GRANT-001 AC2 and AUTHZ-GRANT-003 AC2: a grant on the whole organization is
    /// written with no resource, and records who granted it, when and why.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_001_AC2_AnOrganizationWideGrantIsWrittenWithNoResourceAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "organization", Branch.ToString(), reason: "  Joins the branch.  ");

        Assert.Equal(StatusCodes.Status201Created, created.Status);

        Grant grant = await StoredAsync(created);

        Assert.True(grant.IsOrganizationWide);
        Assert.Equal(Branch, grant.Organization);
        Assert.Equal(Reader, grant.Role);
        Assert.Equal(new GrantSubject(SubjectType.User, Holder), grant.Subject);
        Assert.Equal(GrantKind.Stored, grant.Kind);
        Assert.Equal(actor, grant.GrantedBy);
        Assert.Equal(_deployment.Clock.GetUtcNow(), grant.GrantedAt);
        Assert.Equal("Joins the branch.", grant.Reason);
        Assert.False(grant.Deny);
    }

    /// <summary>
    /// IDN-LIFE-009a AC1 and INT-MAIL-006: a grant in the administrative organization is
    /// a grant and nothing more: the holder is no member of it afterwards and no
    /// mailbox is reserved, since staff membership begins at an invitation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AC1_AGrantInTheAdministrativeOrganizationMakesNoMemberAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "organization", Administration.ToString());

        Assert.Equal(Administration, (await StoredAsync(created)).Organization);
        Assert.Empty(await _deployment.Memberships.OfAsync(new SubjectId(Holder), CancellationToken.None));
        Assert.Empty(_deployment.Mailboxes.Held);
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: a grant on a record is scoped to the organization the record
    /// was registered in, and that is where the permission is asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_ARecordGrantIsScopedToTheRecordsOrganizationAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1");

        Assert.Equal(StatusCodes.Status201Created, created.Status);

        Grant grant = await StoredAsync(created);

        Assert.Equal(Branch, grant.Organization);
        Assert.Equal(Document.Type, grant.ResourceType);
        Assert.Equal(Document.Id, grant.ResourceId);
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 and AUTHZ-CONCEAL-005: <c>grant:manage</c> held in another
    /// organization does not reach a record of this one, and the refusal is forbidden.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_GrantManageElsewhereDoesNotReachTheRecordAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1");

        Assert.Equal(StatusCodes.Status403Forbidden, created.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), created.Text("code"));
        Assert.Empty(await HeldAsync(Branch));
    }

    /// <summary>
    /// AUTHZ-GRANT-002: a deny is the same row with its flag set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_002_ADenyIsTheSameRowWithItsFlagAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1", deny: true);

        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.True((await StoredAsync(created)).Deny);
    }

    /// <summary>
    /// AUTHZ-GRANT-003: an identical live grant is a duplicate, and a second one is not
    /// written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AnIdenticalLiveGrantIsADuplicateAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        _ = await GrantedAsync(administrator, "document", "d-1");
        Answer again = await GrantedAsync(administrator, "document", "d-1");

        Assert.Equal(StatusCodes.Status409Conflict, again.Status);
        Assert.Equal(ErrorCodes.GrantDuplicate.ToString(), again.Text("code"));
        Assert.Single(await HeldAsync(Branch));
    }

    /// <summary>
    /// AUTHZ-GRANT-003: an expiry is optional and is recorded, and one that has passed
    /// already is refused rather than written as a grant that confers nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AnExpiryIsRecordedAndOneAlreadyPassedIsRefusedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Answer expiring = await GrantedAsync(administrator, "document", "d-1", expiresAt: now.AddDays(30));
        Answer passed = await GrantedAsync(administrator, "organization", Branch.ToString(), expiresAt: now);

        Assert.Equal(StatusCodes.Status201Created, expiring.Status);
        Assert.Equal(now.AddDays(30), (await StoredAsync(expiring)).ExpiresAt);
        Assert.Equal(StatusCodes.Status409Conflict, passed.Status);
        Assert.Equal(ErrorCodes.GrantExpired.ToString(), passed.Text("code"));
    }

    /// <summary>
    /// AUTHZ-GRANT-003: a grant and a revocation without a reason are refused with the
    /// code chapter 10 gives, and nothing changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_ABlankReasonIsRefusedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer unreasoned = await GrantedAsync(administrator, "document", "d-1", reason: "   ");
        Answer created = await GrantedAsync(administrator, "document", "d-1");
        Answer unrevoked = await administrator.SendAsync(
            "DELETE",
            "/admin/grants/" + created.Text("id"),
            ("reason", null));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreasoned.Status);
        Assert.Equal(ErrorCodes.GrantReasonRequired.ToString(), unreasoned.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unrevoked.Status);
        Assert.Equal(ErrorCodes.GrantReasonRequired.ToString(), unrevoked.Text("code"));
        Assert.Null((await StoredAsync(created)).RevokedAt);
    }

    /// <summary>
    /// API-CONV-002: a reason is at most 1024 characters after trimming, and a longer
    /// one is a request the boundary does not read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AReasonPastTheLimitIsMalformedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer longest = await GrantedAsync(administrator, "document", "d-1", reason: new string('r', 1024));
        Answer longer = await GrantedAsync(administrator, "organization", Branch.ToString(), reason: new string('r', 1025));

        Assert.Equal(StatusCodes.Status201Created, longest.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, longer.Status);
        Assert.Equal("reason", Member(longer));
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC2: a revocation records who revoked the grant, when and why,
    /// and leaves the row in place.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC2_TheRevocationRecordsWhoWhenAndWhyAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1");

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        Answer revoked = await administrator.SendAsync(
            "DELETE",
            "/admin/grants/" + created.Text("id"),
            ("reason", "Left the project."));

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);

        Grant grant = await StoredAsync(created);

        Assert.Equal(actor, grant.RevokedBy);
        Assert.Equal(_deployment.Clock.GetUtcNow(), grant.RevokedAt);
        Assert.Equal("Left the project.", grant.RevocationReason);
        Assert.Empty(await HeldAsync(Branch));
    }

    /// <summary>
    /// AUTHZ-GRANT-001: a revocation names a stored grant that stands unrevoked; any
    /// other identifier is no such grant.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_001_OnlyAnUnrevokedStoredGrantIsRevokedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1");
        Grant materialised = await MaterialisedAsync();

        Answer first = await RevokedAsync(administrator, created.Text("id"));
        Answer second = await RevokedAsync(administrator, created.Text("id"));
        Answer unknown = await RevokedAsync(administrator, Guid.NewGuid().ToString());
        Answer derived = await RevokedAsync(administrator, materialised.Id.ToString());

        Assert.Equal(StatusCodes.Status204NoContent, first.Status);
        Assert.Equal(StatusCodes.Status404NotFound, second.Status);
        Assert.Equal(ErrorCodes.GrantNotFound.ToString(), second.Text("code"));
        Assert.Equal(StatusCodes.Status404NotFound, unknown.Status);
        Assert.Equal(StatusCodes.Status404NotFound, derived.Status);
        Assert.Null((await _deployment.AccessGrants.FindAsync(materialised.Id, CancellationToken.None))!.RevokedAt);
    }

    /// <summary>
    /// OPS-CFG-007 AC1: a grant-managing administrator without system administration
    /// cannot confer it, allow or deny, and one holding it can.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_007_AC1_AGrantManagerWithoutSystemAdministerCannotConferItAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Administration, Permissions.GrantManage);
        string organization = Administration.ToString();

        Answer conferred = await GrantedAsync(administrator, "organization", organization, role: SystemAdministrator);
        Answer denied = await GrantedAsync(administrator, "organization", organization, role: SystemAdministrator, deny: true);

        _deployment.Gate.Grant(actor, Administration, Permissions.SystemAdminister);

        Answer administered = await GrantedAsync(administrator, "organization", organization, role: SystemAdministrator);

        Assert.Equal(StatusCodes.Status403Forbidden, conferred.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), conferred.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, denied.Status);
        Assert.Equal(StatusCodes.Status201Created, administered.Status);
    }

    /// <summary>
    /// OPS-CFG-007: revoking system administration requires system administration.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_007_RevokingSystemAdministrationRequiresItAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(
            Administration,
            Permissions.GrantManage,
            Permissions.SystemAdminister);

        Answer created = await GrantedAsync(
            administrator,
            "organization",
            Administration.ToString(),
            role: SystemAdministrator);

        _deployment.Gate.Revoke(actor, Administration, Permissions.SystemAdminister);

        Answer refused = await RevokedAsync(administrator, created.Text("id"));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        Assert.Null((await StoredAsync(created)).RevokedAt);
    }

    /// <summary>
    /// AUTH-STEP-001 and chapter 10 section 5a: granting and revoking are the
    /// <c>grant:manage</c> step-up action, so a session whose proof is no longer recent
    /// changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_GrantingAndRevokingAreStepUpActionsAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(administrator, "document", "d-1");

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer granted = await GrantedAsync(administrator, "organization", Branch.ToString());
        Answer revoked = await RevokedAsync(administrator, created.Text("id"));

        Assert.Equal(StatusCodes.Status403Forbidden, granted.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), granted.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, revoked.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), revoked.Text("code"));
        Assert.Single(await HeldAsync(Branch));
    }

    /// <summary>
    /// AUTHZ-GRANT-001: a grant names a role, a registered record or an organization,
    /// and a group of the grant's own organization; anything else is a malformed
    /// request naming the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_001_WhatAGrantNamesMustExistAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage);
        var local = Group.Create(new GroupId(Guid.NewGuid()), Branch, "Tellers");
        var foreign = Group.Create(new GroupId(Guid.NewGuid()), Administration, "Operators");

        await _deployment.Groups.CreateAsync(local, CancellationToken.None);
        await _deployment.Groups.CreateAsync(foreign, CancellationToken.None);

        Answer unregistered = await GrantedAsync(administrator, "document", "d-2");
        Answer unnamed = await GrantedAsync(administrator, "organization", "not-an-organization");
        Answer unknownRole = await GrantedAsync(administrator, "document", "d-1", role: RoleName.Parse("auditor"));
        Answer foreignGroup = await GrantedAsync(administrator, "document", "d-1", group: foreign.Id);
        Answer localGroup = await GrantedAsync(administrator, "document", "d-1", group: local.Id);
        Answer untyped = await administrator.SendAsync(
            "POST",
            "/admin/grants",
            ("subjectType", "user"),
            ("subjectId", Holder),
            ("resourceType", "Not A Type"),
            ("resourceId", "d-1"),
            ("role", Reader.ToString()),
            ("reason", "Needs it."));

        Assert.Equal("resourceId", Member(unregistered));
        Assert.Equal("resourceId", Member(unnamed));
        Assert.Equal("role", Member(unknownRole));
        Assert.Equal("subjectId", Member(foreignGroup));
        Assert.Equal("resourceType", Member(untyped));
        Assert.Equal(StatusCodes.Status201Created, localGroup.Status);
        Assert.Equal(
            new GrantSubject(SubjectType.Group, local.Id.Value),
            (await StoredAsync(localGroup)).Subject);
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC3: the live grants one user holds in its own name are read
    /// oldest first, each with who granted it, when and why; an organization-wide grant
    /// names the organization, and revoked, expired and group grants are left out.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC3_WhoGrantedWhatAHolderHoldsAndWhenIsReadAsync()
    {
        (Browser administrator, SubjectId actor) =
            await AuthorisedAsync(Branch, Permissions.GrantManage, Permissions.GrantRead);
        var tellers = Group.Create(new GroupId(Guid.NewGuid()), Branch, "Tellers");

        await _deployment.Groups.CreateAsync(tellers, CancellationToken.None);

        DateTimeOffset first = _deployment.Clock.GetUtcNow();
        Answer onRecord = await GrantedAsync(administrator, "document", "d-1", expiresAt: first.AddDays(30));

        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        DateTimeOffset second = _deployment.Clock.GetUtcNow();
        Answer wide = await GrantedAsync(administrator, "organization", Branch.ToString(), deny: true, reason: "Suspended pending review.");
        Answer revoked = await GrantedAsync(administrator, "document", "d-1", deny: true);
        Answer expired = await GrantedAsync(administrator, "organization", Branch.ToString(), expiresAt: second.AddMinutes(2));
        Answer toGroup = await GrantedAsync(administrator, "document", "d-1", group: tellers.Id);

        Assert.Equal(StatusCodes.Status204NoContent, (await RevokedAsync(administrator, revoked.Text("id"))).Status);
        Assert.Equal(StatusCodes.Status201Created, expired.Status);
        Assert.Equal(StatusCodes.Status201Created, toGroup.Status);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(3));

        Answer read = await ReadAsync(administrator, "user", Holder.ToString());

        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement[] held = [.. read.Json().EnumerateArray()];

        Assert.Equal([onRecord.Text("id"), wide.Text("id")], held.Select(grant => grant.GetProperty("id").GetString()));
        Assert.Equal("stored", held[0].GetProperty("kind").GetString());
        Assert.Equal("user", held[0].GetProperty("subjectType").GetString());
        Assert.Equal(Holder, held[0].GetProperty("subjectId").GetGuid());
        Assert.Equal("document", held[0].GetProperty("resourceType").GetString());
        Assert.Equal("d-1", held[0].GetProperty("resourceId").GetString());
        Assert.Equal(Reader.ToString(), held[0].GetProperty("role").GetString());
        Assert.False(held[0].GetProperty("deny").GetBoolean());
        Assert.Equal(first.AddDays(30), held[0].GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(actor.Value, held[0].GetProperty("grantedBy").GetGuid());
        Assert.Equal(first, held[0].GetProperty("grantedAt").GetDateTimeOffset());
        Assert.Equal("Needs it.", held[0].GetProperty("reason").GetString());
        Assert.Equal("organization", held[1].GetProperty("resourceType").GetString());
        Assert.Equal(Branch.ToString(), held[1].GetProperty("resourceId").GetString());
        Assert.True(held[1].GetProperty("deny").GetBoolean());
        Assert.Equal(JsonValueKind.Null, held[1].GetProperty("expiresAt").ValueKind);
        Assert.Equal(second, held[1].GetProperty("grantedAt").GetDateTimeOffset());
        Assert.Equal("Suspended pending review.", held[1].GetProperty("reason").GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC3: a group's grants are read under the group, and a grant a
    /// materialisation wrote is read with its kind, which tells apart the one no
    /// revocation names.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC3_AGroupsAndAMaterialisedGrantAreReadWithTheirKindAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantManage, Permissions.GrantRead);
        var tellers = Group.Create(new GroupId(Guid.NewGuid()), Branch, "Tellers");

        await _deployment.Groups.CreateAsync(tellers, CancellationToken.None);

        Answer toGroup = await GrantedAsync(administrator, "document", "d-1", group: tellers.Id);
        Grant materialised = await MaterialisedAsync();

        Answer group = await ReadAsync(administrator, "group", tellers.Id.ToString());
        Answer user = await ReadAsync(administrator, "user", Holder.ToString());

        JsonElement grouped = Assert.Single(group.Json().EnumerateArray());
        JsonElement derived = Assert.Single(user.Json().EnumerateArray());

        Assert.Equal(toGroup.Text("id"), grouped.GetProperty("id").GetString());
        Assert.Equal("group", grouped.GetProperty("subjectType").GetString());
        Assert.Equal(tellers.Id.Value, grouped.GetProperty("subjectId").GetGuid());
        Assert.Equal(materialised.Id.ToString(), derived.GetProperty("id").GetString());
        Assert.Equal("materialised", derived.GetProperty("kind").GetString());
    }

    /// <summary>
    /// Entry 268 and AUTHZ-CONCEAL-005: reading what a holder holds needs
    /// <c>grant:read</c> in that organization; <c>grant:manage</c> alone, or
    /// <c>grant:read</c> elsewhere, is forbidden.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC3_ReadingWhatAHolderHoldsNeedsGrantReadThereAsync()
    {
        (Browser manager, SubjectId actor) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Answer created = await GrantedAsync(manager, "document", "d-1");
        Answer unread = await ReadAsync(manager, "user", Holder.ToString());

        _deployment.Gate.Grant(actor, Administration, Permissions.GrantRead);

        Answer foreign = await ReadAsync(manager, "user", Holder.ToString());

        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, unread.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), unread.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, foreign.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), foreign.Text("code"));
    }

    /// <summary>
    /// API-CONV-002: a read of held grants names an organization, a user or group, and
    /// its identifier; anything else is a malformed request naming the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AReadOfHeldGrantsNamesWhoseAndWhereAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch, Permissions.GrantRead);

        Answer unscoped = await administrator.SendAsync("GET", "/admin/grants?subjectType=user&subjectId=" + Holder);
        Answer untyped = await administrator.SendAsync("GET", "/admin/grants?organization=" + Branch + "&subjectType=role&subjectId=" + Holder);
        Answer unnamed = await administrator.SendAsync("GET", "/admin/grants?organization=" + Branch + "&subjectType=user&subjectId=d-1");

        Assert.Equal("organization", Member(unscoped));
        Assert.Equal("subjectType", Member(untyped));
        Assert.Equal("subjectId", Member(unnamed));
    }

    private static Task<Answer> ReadAsync(Browser administrator, string subjectType, string subjectId) =>
        administrator.SendAsync(
            "GET",
            "/admin/grants?organization=" + Branch + "&subjectType=" + subjectType + "&subjectId=" + subjectId);

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private static Task<Answer> RevokedAsync(Browser administrator, string id) =>
        administrator.SendAsync("DELETE", "/admin/grants/" + id, ("reason", "No longer needed."));

    private static Task<Answer> GrantedAsync(
        Browser administrator,
        string resourceType,
        string resourceId,
        RoleName? role = null,
        bool deny = false,
        DateTimeOffset? expiresAt = null,
        GroupId? group = null,
        string reason = "Needs it.") =>
        administrator.SendAsync(
            "POST",
            "/admin/grants",
            ("subjectType", group is null ? "user" : "group"),
            ("subjectId", group?.Value ?? Holder),
            ("resourceType", resourceType),
            ("resourceId", resourceId),
            ("role", (role ?? Reader).ToString()),
            ("deny", deny),
            ("expiresAt", expiresAt),
            ("reason", reason));

    private async Task<Grant> StoredAsync(Answer created)
    {
        Assert.Equal(StatusCodes.Status201Created, created.Status);

        return (await _deployment.AccessGrants.FindAsync(
            new GrantId(Guid.Parse(created.Text("id"))),
            CancellationToken.None))!;
    }

    private async Task<IReadOnlyList<Grant>> HeldAsync(OrganizationId organization) =>
        await _deployment.AccessGrants.HeldByAsync(
            [new GrantSubject(SubjectType.User, Holder)],
            organization,
            _deployment.Clock.GetUtcNow(),
            CancellationToken.None);

    // A grant a derivation's refresh wrote, which the host's data answers for.
    private async Task<Grant> MaterialisedAsync()
    {
        Grant materialised = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                new GrantSubject(SubjectType.User, Holder),
                Reader,
                Branch,
                Document,
                deny: false,
                GrantKind.Materialised,
                expiresAt: null,
                new SubjectId(Holder),
                _deployment.Clock.GetUtcNow(),
                "Refreshed from the host's data.")
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(materialised, CancellationToken.None);

        return materialised;
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
