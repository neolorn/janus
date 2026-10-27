using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Nobody hands out a role beyond what they may hand out, and nobody gives themselves
/// access, judged by the gate over the grants a deployment holds (AUTHZ-GRANT-005, D-212,
/// D-216).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
/// <remarks>
/// Each case writes a deployment of its own; the cases that turn handing out by holding
/// on put the default back before they end, so no other case runs under it.
/// </remarks>
[Trait("kind", "integration")]
public sealed class HandOutTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    private static readonly RoleName Reviewer = RoleName.Parse("reviewer");

    /// <summary>
    /// AUTHZ-GRANT-005 AC1: with handing out by holding off, an actor managing grants and
    /// holding a role whose <c>handsOut</c> names a role grants it to another account; a
    /// role no role of the actor lists is refused <c>authz.role.notassignable</c>,
    /// recorded as a refusal, and nothing is granted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC1_ARoleTheActorsRoleListsIsHandedOutAndNoOtherAsync()
    {
        Case held = await CaseAsync();
        SubjectId account = await held.Deployment.AccountAsync(TestContext.Current.CancellationToken);
        RoleName unlisted = await held.Deployment.RoleAsync([HostPermissions.Read], TestContext.Current.CancellationToken);
        int refusals = await RefusalsAsync(held.Actor);

        Result<GrantId> listed = await GrantedAsync(held, GrantSubject.Of(account), held.Listed);
        Result<GrantId> refused = await GrantedAsync(held, GrantSubject.Of(account), unlisted);

        Assert.Null(Code(listed));
        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(refused));
        Assert.Equal(1, await LiveGrantsAsync(account));
        Assert.Equal(refusals + 1, await RefusalsAsync(held.Actor));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC1: with handing out by holding off, the actor adds a member to a
    /// group holding the role its role lists; a group holding a role no role of the actor
    /// lists, itself or through a group it belongs to, is refused
    /// <c>authz.role.notassignable</c> and keeps its members as they were.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC1_AGroupReachingOnlyRolesTheActorHandsOutTakesAMemberAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        RoleName unlisted = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        GroupId listing = await deployment.GroupAsync(cancellationToken);
        GroupId holding = await deployment.GroupAsync(cancellationToken);
        GroupId inner = await deployment.GroupAsync(cancellationToken);

        await deployment.GrantAsync(GrantSubject.Of(listing), held.Listed, null, false, null, null, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(holding), unlisted, null, false, null, null, cancellationToken);
        await deployment.JoinAsync(holding, GrantSubject.Of(inner), cancellationToken);

        Result added = await MemberChangedAsync(held, listing, GrantSubject.Of(account), adding: true);
        Result refused = await MemberChangedAsync(held, holding, GrantSubject.Of(account), adding: true);
        Result nested = await MemberChangedAsync(held, inner, GrantSubject.Of(account), adding: true);

        Assert.Null(Code(added));
        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(refused));
        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(nested));
        Assert.Equal(1, await MembersAsync(listing));
        Assert.Equal(1, await MembersAsync(holding));
        Assert.Equal(0, await MembersAsync(inner));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC2: with handing out by holding on, an actor whose grants admit
    /// every permission of a role hands it out with no role listing it; one whose grants
    /// miss a permission of it, or deny one, is refused. Off, the same role is refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC2_ByHoldingARoleIsHandedOutWhereTheGrantsAdmitAllItCarriesAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        RoleName holding = await deployment.RoleAsync([HostPermissions.Read, HostPermissions.Edit], cancellationToken);
        RoleName denying = await deployment.RoleAsync([HostPermissions.Edit], cancellationToken);
        RoleName reading = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        RoleName publishing = await deployment.RoleAsync([HostPermissions.Read, HostPermissions.Publish], cancellationToken);
        RoleName editing = await deployment.RoleAsync([HostPermissions.Edit], cancellationToken);

        await deployment.GrantAsync(GrantSubject.Of(held.Actor), holding, null, false, null, null, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(held.Actor), denying, null, true, null, null, cancellationToken);

        Result<GrantId> off = await GrantedAsync(held, GrantSubject.Of(account), reading);

        await ByHoldingAsync(true);

        try
        {
            Result<GrantId> admitted = await GrantedAsync(held, GrantSubject.Of(account), reading);
            Result<GrantId> missing = await GrantedAsync(held, GrantSubject.Of(account), publishing);
            Result<GrantId> denied = await GrantedAsync(held, GrantSubject.Of(account), editing);

            Assert.Equal(ErrorCodes.RoleNotAssignable, Code(off));
            Assert.Null(Code(admitted));
            Assert.Equal(ErrorCodes.RoleNotAssignable, Code(missing));
            Assert.Equal(ErrorCodes.RoleNotAssignable, Code(denied));
            Assert.Equal(1, await LiveGrantsAsync(account));
        }
        finally
        {
            await ByHoldingAsync(false);
        }
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC3: an allow grant to oneself, one to a group one belongs to
    /// through nesting and the revocation of a deny on oneself are each refused
    /// <c>authz.grant.self</c>, though the actor may hand the role out; revoking one's own
    /// allow grant is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC3_AGrantWideningTheActorsOwnAccessIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        GroupId inner = await deployment.GroupAsync(cancellationToken);
        GroupId outer = await deployment.GroupAsync(cancellationToken);

        await deployment.JoinAsync(inner, GrantSubject.Of(held.Actor), cancellationToken);
        await deployment.JoinAsync(outer, GrantSubject.Of(inner), cancellationToken);

        GrantId denial = await deployment.GrantAsync(
            GrantSubject.Of(held.Actor), held.Listed, null, true, null, null, cancellationToken);
        GrantId allowed = await deployment.GrantAsync(
            GrantSubject.Of(held.Actor), held.Listed, null, false, null, null, cancellationToken);

        Result<GrantId> own = await GrantedAsync(held, GrantSubject.Of(held.Actor), held.Listed);
        Result<GrantId> nested = await GrantedAsync(held, GrantSubject.Of(outer), held.Listed);
        Result undenied = await RevokedAsync(held, denial);
        Result given = await RevokedAsync(held, allowed);

        Assert.Equal(ErrorCodes.GrantSelf, Code(own));
        Assert.Equal(ErrorCodes.GrantSelf, Code(nested));
        Assert.Equal(ErrorCodes.GrantSelf, Code(undenied));
        Assert.Null(Code(given));
        // The managing grant and the deny stand; the allow is revoked.
        Assert.Equal(2, await LiveGrantsAsync(held.Actor));
        Assert.Equal(0, await LiveGrantsAsync(outer.Value));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC3: adding oneself to a group, or a group one belongs to, is
    /// refused <c>authz.grant.self</c> whatever the group holds, and so is leaving a group
    /// that reaches a deny; leaving one that reaches only an allow is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC3_JoiningAGroupOrLeavingADenyIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        GroupId empty = await deployment.GroupAsync(cancellationToken);
        GroupId own = await deployment.GroupAsync(cancellationToken);
        GroupId denying = await deployment.GroupAsync(cancellationToken);
        GroupId allowing = await deployment.GroupAsync(cancellationToken);

        await deployment.JoinAsync(own, GrantSubject.Of(held.Actor), cancellationToken);
        await deployment.JoinAsync(denying, GrantSubject.Of(held.Actor), cancellationToken);
        await deployment.JoinAsync(allowing, GrantSubject.Of(held.Actor), cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(denying), held.Listed, null, true, null, null, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(allowing), held.Listed, null, false, null, null, cancellationToken);

        Result joined = await MemberChangedAsync(held, empty, GrantSubject.Of(held.Actor), adding: true);
        Result nested = await MemberChangedAsync(held, empty, GrantSubject.Of(own), adding: true);
        Result undenied = await MemberChangedAsync(held, denying, GrantSubject.Of(held.Actor), adding: false);
        Result left = await MemberChangedAsync(held, allowing, GrantSubject.Of(held.Actor), adding: false);

        Assert.Equal(ErrorCodes.GrantSelf, Code(joined));
        Assert.Equal(ErrorCodes.GrantSelf, Code(nested));
        Assert.Equal(ErrorCodes.GrantSelf, Code(undenied));
        Assert.Null(Code(left));
        Assert.Equal(0, await MembersAsync(empty));
        Assert.Equal(1, await MembersAsync(denying));
        Assert.Equal(0, await MembersAsync(allowing));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC4: a role change adding a permission the actor's grants in the
    /// administrative organization do not admit, and one whose <c>handsOut</c> names a
    /// role the actor may not hand out there, are refused
    /// <c>authz.role.notassignable</c> and change nothing; a change within the actor's
    /// reach is made.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC4_ARoleChangeBeyondTheActorsReachIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName listed = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.RoleManage, HostPermissions.Read], cancellationToken);
        RoleName unlisted = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        SubjectId actor = await deployment.AccountAsync(cancellationToken);
        var drafted = RoleName.Parse("drafted-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        await deployment.MemberAsync(actor, administrative, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(actor), managing, null, false, null, administrative, cancellationToken);
        await deployment.HandsOutAsync(managing, [listed], cancellationToken);

        Result<bool> widened = await DefinedAsync(actor, new DefinedRole(drafted, [HostPermissions.Read, HostPermissions.Edit], []));
        Result<bool> unassignable = await DefinedAsync(actor, new DefinedRole(drafted, [HostPermissions.Read], [unlisted]));
        Result<bool> reached = await DefinedAsync(actor, new DefinedRole(drafted, [HostPermissions.Read], [listed]));

        Assert.Equal(ErrorCodes.RoleNotAssignable, widened.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(ErrorCodes.RoleNotAssignable, unassignable.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.True(reached.Match(created => created, error => throw new InvalidOperationException(error.Code.ToString())));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC5: a deny of a role, and the revocation of a grant of it, are
    /// judged as a grant is, so an actor no role of whose lists the role is refused both
    /// and nothing changes.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC5_ADenyAndARevocationAreJudgedAsAGrantIsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        RoleName unlisted = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        GrantId standing = await deployment.GrantAsync(
            GrantSubject.Of(account), unlisted, null, false, null, null, cancellationToken);

        Result<GrantId> denied = await GrantedAsync(held, GrantSubject.Of(account), unlisted, deny: true);
        Result revoked = await RevokedAsync(held, standing);

        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(denied));
        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(revoked));
        Assert.Equal(1, await LiveGrantsAsync(account));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC5: taking a member out of a group is judged as a grant is, so an
    /// actor no role of whose lists a role the group holds is refused and the member stays.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC5_ARemovalFromAGroupIsJudgedAsAGrantIsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        RoleName unlisted = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        GroupId group = await deployment.GroupAsync(cancellationToken);

        await deployment.GrantAsync(GrantSubject.Of(group), unlisted, null, false, null, null, cancellationToken);
        await deployment.JoinAsync(group, GrantSubject.Of(account), cancellationToken);

        Result removed = await MemberChangedAsync(held, group, GrantSubject.Of(account), adding: false);

        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(removed));
        Assert.Equal(1, await MembersAsync(group));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC6: an actor holding <c>system:administer</c> in the
    /// administrative organization hands out a role no role of its lists, to itself
    /// included, and defines a role carrying a permission its grants do not admit and
    /// handing out a role no role of its lists.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC6_ASystemAdministratorHandsOutAnyRoleToItselfIncludedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName administering = await deployment.RoleAsync(
            [Permissions.SystemAdminister, Permissions.RoleManage],
            cancellationToken);
        RoleName unlisted = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        SubjectId actor = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(actor, administrative, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(actor), administering, null, false, null, administrative, cancellationToken);

        var held = new Case(deployment, actor, unlisted);
        var drafted = RoleName.Parse("drafted-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Result<GrantId> own = await GrantedAsync(held, GrantSubject.Of(actor), unlisted);
        Result<bool> defined = await DefinedAsync(
            actor,
            new DefinedRole(drafted, [HostPermissions.Read, HostPermissions.Edit], [unlisted]));

        Assert.Null(Code(own));
        Assert.True(defined.Match(created => created, error => throw new InvalidOperationException(error.Code.ToString())));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC7 (D-216): with handing out by holding on, an actor admitted to a
    /// permission on a record only through a live derivation is refused a role carrying
    /// it there with <c>authz.role.notassignable</c>, not a fault; once the derivation's
    /// grant is materialised, the actor hands the role out.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC7_ALiveDerivationAdmitsNothingAndAMaterialisedOneDoesAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        var workspace = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        RoleName reading = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);

        await deployment.NamedRoleAsync(Reviewer, [HostPermissions.Read], cancellationToken);
        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.ReviewAsync(workspace, held.Actor, cancellationToken);
        await ByHoldingAsync(true);

        try
        {
            Result<GrantId> live = await GrantedAsync(held, GrantSubject.Of(account), reading, workspace);

            await deployment.MaterialisedAsync(GrantSubject.Of(held.Actor), Reviewer, workspace, cancellationToken);

            Result<GrantId> materialised = await GrantedAsync(held, GrantSubject.Of(account), reading, workspace);

            Assert.Equal(ErrorCodes.RoleNotAssignable, Code(live));
            Assert.Null(Code(materialised));
        }
        finally
        {
            await ByHoldingAsync(false);
        }
    }

    /// <summary>
    /// AUTHZ-GRANT-005 AC8: with handing out by holding off, a role's <c>handsOut</c>
    /// counts only where no live deny of that role to the actor, or to a group it belongs
    /// to, covers the change: a deny across the organization, or on the change's record
    /// or a container above it, refuses the role the list names with
    /// <c>authz.role.notassignable</c>; a deny on a record covers no change across the
    /// organization; with the denies revoked, the actor hands the role out.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AC8_ADenyOfARoleDefeatsTheRolesItHandsOutAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        Deployment deployment = held.Deployment;
        var folder = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));
        var page = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        SubjectId other = await deployment.AccountAsync(cancellationToken);
        RoleName accountant = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        RoleName personnel = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        GroupId denied = await deployment.GroupAsync(cancellationToken);

        await deployment.RegisterAsync(folder, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(page, folder, cancellationToken);
        await deployment.HandsOutAsync(personnel, [accountant], cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(held.Actor), personnel, null, false, null, null, cancellationToken);
        await deployment.JoinAsync(denied, GrantSubject.Of(held.Actor), cancellationToken);

        GrantId above = await deployment.GrantAsync(
            GrantSubject.Of(denied), personnel, folder, true, null, null, cancellationToken);

        Result<GrantId> underTheDeny = await GrantedAsync(held, GrantSubject.Of(account), accountant, page);
        Result<GrantId> beyondIt = await GrantedAsync(held, GrantSubject.Of(account), accountant);

        GrantId across = await deployment.GrantAsync(
            GrantSubject.Of(held.Actor), personnel, null, true, null, null, cancellationToken);

        Result<GrantId> acrossTheOrganization = await GrantedAsync(held, GrantSubject.Of(other), accountant);

        await deployment.RevokeAsync(above, cancellationToken);
        await deployment.RevokeAsync(across, cancellationToken);

        Result<GrantId> revoked = await GrantedAsync(held, GrantSubject.Of(other), accountant, page);

        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(underTheDeny));
        Assert.Null(Code(beyondIt));
        Assert.Equal(ErrorCodes.RoleNotAssignable, Code(acrossTheOrganization));
        Assert.Null(Code(revoked));
        Assert.Equal(1, await LiveGrantsAsync(account));
        Assert.Equal(1, await LiveGrantsAsync(other));
    }

    private static ErrorCode? Code(Result<GrantId> result) =>
        result.Match<ErrorCode?>(_ => null, error => error.Code);

    private static ErrorCode? Code(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

    private static ResourceReference Whole(Deployment deployment) =>
        new(ResourceType.Parse("organization"), ResourceId.Parse(deployment.Organization.ToString()));

    private static async Task<SessionId> SteppedUpAsync(IServiceProvider services, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            caller,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Deployment.Noon - TimeSpan.FromMinutes(1),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(caller, cancellationToken) is null)
        {
            await keys.CreateAsync(caller, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return session.Id;
    }

    // A deployment whose actor manages grants, groups and their reading across the
    // organization through a role that hands out the case's role.
    private async Task<Case> CaseAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName listed = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Edit], cancellationToken);
        RoleName managing = await deployment.RoleAsync(
            [Permissions.GrantManage, Permissions.GrantRead, Permissions.GroupManage],
            cancellationToken);
        SubjectId actor = await deployment.AccountAsync(cancellationToken);

        await deployment.GrantAsync(GrantSubject.Of(actor), managing, null, false, null, null, cancellationToken);
        await deployment.HandsOutAsync(managing, [listed], cancellationToken);

        return new Case(deployment, actor, listed);
    }

    private async Task<Result<GrantId>> GrantedAsync(
        Case held,
        GrantSubject holder,
        RoleName role,
        ResourceReference? on = null,
        bool deny = false)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return await scope.ServiceProvider.GetRequiredService<IGrants>().GrantAsync(
            AccessContext.Of(held.Actor),
            session,
            new GrantRequest(holder, role, on ?? Whole(held.Deployment), deny, ExpiresAt: null, "Handed out."),
            TestContext.Current.CancellationToken);
    }

    private async Task<Result> RevokedAsync(Case held, GrantId grant)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return await scope.ServiceProvider.GetRequiredService<IGrants>().RevokeAsync(
            AccessContext.Of(held.Actor),
            session,
            grant,
            "No longer needed.",
            TestContext.Current.CancellationToken);
    }

    private async Task<Result<bool>> DefinedAsync(SubjectId actor, DefinedRole role)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, actor);

        return await scope.ServiceProvider.GetRequiredService<IRoles>().DefineAsync(
            AccessContext.Of(actor),
            session,
            role,
            "A role for the drafters.",
            TestContext.Current.CancellationToken);
    }

    private async Task<Result> MemberChangedAsync(Case held, GroupId group, GrantSubject member, bool adding)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);
        IGroups groups = scope.ServiceProvider.GetRequiredService<IGroups>();
        var context = AccessContext.Of(held.Actor);

        return adding
            ? await groups.AddMemberAsync(context, session, group, member, "Joined.", TestContext.Current.CancellationToken)
            : await groups.RemoveMemberAsync(context, session, group, member, "Left.", TestContext.Current.CancellationToken);
    }

    private async Task<int> MembersAsync(GroupId group)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.group_members WHERE group_id = @group;",
            new { group = group.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // AUTHZ-CONCEAL-004: the refusals recorded under the actor.
    private async Task<int> RefusalsAsync(SubjectId actor)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND acting_subject = @actor;
            """,
            new { actor = actor.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private Task<int> LiveGrantsAsync(SubjectId holder) => LiveGrantsAsync(holder.Value);

    private async Task<int> LiveGrantsAsync(Guid holder)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE subject_id = @holder AND revoked_at IS NULL;",
            new { holder },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // AUTHZ-GRANT-005: the setting is the deployment's, written where the configuration
    // store reads it, and its default is what no row gives.
    private async Task ByHoldingAsync(bool on)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            on
                ? "INSERT INTO identity.settings (key, value) VALUES (@key, @value);"
                : "DELETE FROM identity.settings WHERE key = @key;",
            new { key = Settings.AuthzHandoutByHolding.Key.ToString(), value = Settings.AuthzHandoutByHolding.Write(true) },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // One case's deployment, its actor and the role the actor's managing role hands out.
    private sealed record Case(Deployment Deployment, SubjectId Actor, RoleName Listed);
}
