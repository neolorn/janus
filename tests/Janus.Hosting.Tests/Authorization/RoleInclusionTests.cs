using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A role's inclusions of other roles on PostgreSQL: what a grant of the including role
/// admits through the view every decision reads, and what the administration of an
/// inclusion refuses (AUTHZ-GRANT-006, D-278).
/// </summary>
/// <remarks>
/// The cases of this class run one after the other on a database of their own, so the
/// case that switches <c>authz.roleinclusion.enabled</c> off decides no other class's
/// case.
/// </remarks>
[Trait("kind", "integration")]
public sealed class RoleInclusionTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// AUTHZ-GRANT-006 AC1, AC4: a holder of a role including another, which includes a
    /// third, is admitted to what the third carries; taking the permission out of the third
    /// refuses them at the next request, and nothing is written to the roles above it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC4_AChangeToTheLowestRoleReachesTheHoldersAtTheNextRequestAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName lowest = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName middle = await deployment.RoleAsync([], cancellationToken);
        RoleName top = await deployment.RoleAsync([], cancellationToken);
        SubjectId holder = await deployment.AccountAsync(cancellationToken);

        await deployment.IncludesAsync(top, middle, cancellationToken);
        await deployment.IncludesAsync(middle, lowest, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(holder), top, null, false, null, null, cancellationToken);

        bool before = await AdmittedAsync(deployment, holder, HostPermissions.Read);

        await ExecuteAsync(
            "DELETE FROM identity.role_permissions WHERE role = @role;",
            new { role = lowest.ToString() });

        Assert.True(before);
        Assert.False(await AdmittedAsync(deployment, holder, HostPermissions.Read));
        Assert.Equal(0, await CountAsync(
            "SELECT count(*)::int FROM identity.role_permissions WHERE role IN (@top, @middle);",
            new { top = top.ToString(), middle = middle.ToString() }));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC5: with <c>authz.roleinclusion.enabled</c> off a role admits its own
    /// permissions alone, and on admits its inclusions again; an inclusion switched off
    /// acts alone the same way; neither changes a role's stored permissions or inclusions.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC5_TheKeyAndAnInclusionsSwitchEachTurnInclusionsOffAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName included = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName including = await deployment.RoleAsync([HostPermissions.Edit], cancellationToken);
        SubjectId holder = await deployment.AccountAsync(cancellationToken);

        await deployment.IncludesAsync(including, included, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(holder), including, null, false, null, null, cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO identity.settings (key, value) VALUES ('authz.roleinclusion.enabled', 'false')
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """);

        (bool Read, bool Edit) keyOff = (
            await AdmittedAsync(deployment, holder, HostPermissions.Read),
            await AdmittedAsync(deployment, holder, HostPermissions.Edit));

        await ExecuteAsync("UPDATE identity.settings SET value = 'true' WHERE key = 'authz.roleinclusion.enabled';");

        bool keyOn = await AdmittedAsync(deployment, holder, HostPermissions.Read);

        await ExecuteAsync(
            "UPDATE identity.role_inclusions SET enabled = FALSE WHERE role = @role;",
            new { role = including.ToString() });

        bool switchedOff = await AdmittedAsync(deployment, holder, HostPermissions.Read);

        await ExecuteAsync(
            "UPDATE identity.role_inclusions SET enabled = TRUE WHERE role = @role;",
            new { role = including.ToString() });

        Assert.Equal((false, true), keyOff);
        Assert.True(keyOn);
        Assert.False(switchedOff);
        Assert.True(await AdmittedAsync(deployment, holder, HostPermissions.Read));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*)::int FROM identity.role_permissions WHERE role = @role;",
            new { role = including.ToString() }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*)::int FROM identity.role_inclusions WHERE role = @role;",
            new { role = including.ToString() }));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC6, AC7: through the administration of inclusions, an inclusion is
    /// made and recorded; one that would make a role include itself, at any depth, is
    /// refused <c>authz.role.inclusionloop</c>, and one including a role whose grants must
    /// end in one whose grants need not end <c>authz.role.inclusionmustend</c>; neither
    /// writes anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC6_LoopsAndMustEndBreachesAreRefusedAndWriteNothingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName lowest = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName middle = await deployment.RoleAsync([], cancellationToken);
        RoleName top = await deployment.RoleAsync([], cancellationToken);
        RoleName ending = await deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        SubjectId actor = await AdministratorAsync(deployment);

        await ExecuteAsync(
            "UPDATE identity.roles SET grants_must_end = TRUE WHERE name = @role;",
            new { role = ending.ToString() });

        Result<bool> made = await IncludedAsync(actor, top, middle);
        Result<bool> chained = await IncludedAsync(actor, middle, lowest);
        int written = await InclusionsAsync();
        Result<bool> looped = await IncludedAsync(actor, lowest, top);
        Result<bool> itself = await IncludedAsync(actor, top, top);
        Result<bool> breached = await IncludedAsync(actor, top, ending);

        Assert.True(made.Match(created => created, _ => false));
        Assert.True(chained.Match(created => created, _ => false));
        Assert.Equal(ErrorCodes.RoleInclusionLoop, looped.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.RoleInclusionLoop, itself.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.RoleInclusionMustEnd, breached.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(written, await InclusionsAsync());
        Assert.Equal(2, await CountAsync(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.role.included' AND details ->> 'role' IN (@top, @middle);
            """,
            new { top = top.ToString(), middle = middle.ToString() }));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 AC9: the role's read through the library, which follows the graph
    /// in memory, carries exactly what the view every decision reads gives the role, each
    /// entry once, with its source nearest first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AC9_TheReadAgreesWithTheViewEveryDecisionReadsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName lowest = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Approve], cancellationToken);
        RoleName middle = await deployment.RoleAsync([HostPermissions.Edit, Permissions.SystemAdminister], cancellationToken);
        RoleName top = await deployment.RoleAsync([HostPermissions.Publish], cancellationToken);
        SubjectId actor = await AdministratorAsync(deployment);

        await deployment.IncludesAsync(top, middle, cancellationToken);
        await deployment.IncludesAsync(middle, lowest, cancellationToken, leftOut: [HostPermissions.Edit]);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        DefinedRole read = (await scope.ServiceProvider.GetRequiredService<IRoles>()
                .AllAsync(AccessContext.Of(actor), cancellationToken))
            .Match(all => all.Single(role => role.Name == top), error => throw new InvalidOperationException(error.Code.ToString()));

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> viewed = await connection.QueryAsync<string>(
            "SELECT permission FROM identity.role_effective_permissions WHERE role = @role ORDER BY permission COLLATE \"C\";",
            new { role = top.ToString() });

        Assert.Equal(viewed, read.Effective!.Permissions.Select(entry => entry.Name.ToString()));
        Assert.Equal(
            [
                ("document:edit", middle.ToString()),
                ("document:publish", string.Empty),
                ("document:read", middle + "/" + lowest),
            ],
            read.Effective.Permissions.Select(entry => (entry.Name.ToString(), string.Join('/', entry.From))));
    }

    /// <summary>
    /// AUTHZ-GRANT-006 (D-282): a holder of <c>grant:manage</c> in an organization other
    /// than the administrative one, and of nothing else, reads every role with its
    /// inclusions, its effective permissions and their sources; a change to an inclusion
    /// by them is refused, and a caller holding none of the three permissions is refused
    /// the read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_006_AGiverOfRolesInAnyOrganizationReadsThemReadOnlyAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName lowest = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName top = await deployment.RoleAsync([HostPermissions.Publish], cancellationToken);
        RoleName giving = await deployment.RoleAsync([Permissions.GrantManage], cancellationToken);
        SubjectId giver = await deployment.AccountAsync(cancellationToken);
        SubjectId outsider = await deployment.AccountAsync(cancellationToken);

        _ = await deployment.AdministrativeAsync(cancellationToken);
        await deployment.MemberAsync(giver, deployment.Organization, cancellationToken);
        await deployment.MemberAsync(outsider, deployment.Organization, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(giver), giving, null, false, null, null, cancellationToken);
        await deployment.IncludesAsync(top, lowest, cancellationToken);

        Result<IReadOnlyList<DefinedRole>> read;
        Result<IReadOnlyList<DefinedRole>> refused;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IRoles roles = scope.ServiceProvider.GetRequiredService<IRoles>();

            read = await roles.AllAsync(AccessContext.Of(giver), cancellationToken);
            refused = await roles.AllAsync(AccessContext.Of(outsider), cancellationToken);
        }

        DefinedRole topRead = read.Match(
            all => all.Single(role => role.Name == top),
            error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal([lowest], topRead.Includes!.Select(inclusion => inclusion.Included));
        Assert.Equal(
            [("document:publish", string.Empty), ("document:read", lowest.ToString())],
            topRead.Effective!.Permissions.Select(entry => (entry.Name.ToString(), string.Join('/', entry.From))));
        Assert.Equal(ErrorCodes.Denied, refused.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(
            ErrorCodes.Denied,
            (await IncludedAsync(giver, giving, lowest)).Match<ErrorCode?>(_ => null, error => error.Code));
    }

    // A member of the administrative organization managing inclusions there, holding
    // system administration so every change stays within its reach (AUTHZ-GRANT-005).
    private static async Task<SubjectId> AdministratorAsync(Deployment deployment)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName administering = await deployment.RoleAsync(
            [Permissions.RoleInclusionManage, Permissions.RoleManage, Permissions.SystemAdminister],
            cancellationToken);
        SubjectId actor = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(actor, administrative, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(actor), administering, null, false, null, administrative, cancellationToken);

        return actor;
    }

    private async Task<Result<bool>> IncludedAsync(SubjectId actor, RoleName role, RoleName included)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, actor);

        return await scope.ServiceProvider.GetRequiredService<IRoles>().IncludeAsync(
            AccessContext.Of(actor),
            session,
            role,
            new RoleInclusion(included, Enabled: true, [], []),
            "Managers do what the role below does.",
            TestContext.Current.CancellationToken);
    }

    // A session meeting the grant:manage gate, as a person's own would.
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

    // Whether the single check admits the holder to the permission across the case's
    // organization.
    private async Task<bool> AdmittedAsync(Deployment deployment, SubjectId holder, Permission permission)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(holder), permission, deployment.Organization, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false);
    }

    private async Task<int> InclusionsAsync() =>
        await CountAsync("SELECT count(*)::int FROM identity.role_inclusions;");

    private async Task<int> CountAsync(string statement, object? parameters = null)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(statement, parameters);
    }

    private async Task ExecuteAsync(string statement, object? parameters = null)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        _ = await connection.ExecuteAsync(statement, parameters);
    }
}
