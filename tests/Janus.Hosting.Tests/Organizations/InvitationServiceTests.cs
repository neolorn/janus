using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The end of a membership as the deployment composes it, over the database and the
/// gate itself (IDN-MEM-001, IDN-LIFE-009a).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class InvitationServiceTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Source = "198.51.100.7";

    private const string Why = "The contract ended.";

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// IDN-MEM-001 AC4, IDN-LIFE-009a, D-213: ending an account's membership of the
    /// administrative organization stops what its grants there conferred, revoking them,
    /// and removes no grant row.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC4_EndingTheAdministrativeMembershipStopsItsGrantsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync([Permissions.MembershipManage], cancellationToken);
        RoleName reading = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.HandsOutAsync(managing, [reading], cancellationToken);
        await deployment.MemberAsync(administrator, administrative, cancellationToken);
        await deployment.MemberAsync(member, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator),
            managing,
            null,
            false,
            null,
            administrative,
            cancellationToken);

        GrantId held = await deployment.GrantAsync(
            GrantSubject.Of(member),
            reading,
            null,
            false,
            null,
            administrative,
            cancellationToken);

        Assert.True(await HeldAsync(member, administrative));

        Assert.Null(await EndedAsync(administrator, administrative, member));

        Assert.False(await HeldAsync(member, administrative));
        Assert.Equal(0, await StandingAsync(held));
        Assert.Equal(1, await RowsAsync(held));
    }

    /// <summary>
    /// IDN-MEM-001 AC3, AC9, D-213, D-216: after a membership of an organization ends,
    /// the grant a person made to the account there is revoked with the end's reason, the
    /// account is in none of the organization's groups, a grant a derivation materialised
    /// stands, and every record the end wrote carries the reason.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC3_EndingAMembershipRevokesWhatAPersonGaveAndLeavesItsGroupsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync([Permissions.MembershipManage], cancellationToken);
        RoleName reading = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);
        OrganizationId organization = deployment.Organization;
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);
        GroupId group = await deployment.GroupAsync(cancellationToken);
        var workspace = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));

        await deployment.HandsOutAsync(managing, [reading], cancellationToken);
        await deployment.MemberAsync(administrator, organization, cancellationToken);
        await deployment.MemberAsync(member, organization, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator),
            managing,
            null,
            false,
            null,
            organization,
            cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(group), reading, null, false, null, organization, cancellationToken);
        await deployment.JoinAsync(group, GrantSubject.Of(member), cancellationToken);
        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);

        GrantId held = await deployment.GrantAsync(
            GrantSubject.Of(member),
            reading,
            null,
            false,
            null,
            organization,
            cancellationToken);
        GrantId materialised = await deployment.MaterialisedAsync(
            GrantSubject.Of(member),
            reading,
            workspace,
            cancellationToken);

        Assert.Null(await EndedAsync(administrator, organization, member));

        Assert.Equal(0, await StandingAsync(held));
        Assert.Equal(Why, await RevocationReasonAsync(held));
        Assert.Equal(1, await StandingAsync(materialised));
        Assert.Equal(0, await GroupsHoldingAsync(member, group));
        Assert.Equal(1, await RecordedAsync("authz.grant.revoked", administrator, organization));
        Assert.Equal(1, await RecordedAsync("authz.group.memberremoved", administrator, organization));
        Assert.Equal(1, await RecordedAsync("identity.membership.ended", administrator, organization));
    }

    /// <summary>
    /// IDN-MEM-001 AC3, D-213: after a membership of the administrative organization ends,
    /// the account's grants in an organization it is not a member of are revoked, and
    /// those in one it is still a member of stand.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC3_EndingTheAdministrativeMembershipRevokesGrantsWhereTheAccountIsNoMemberAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);
        var elsewhere = new Authorization.Deployment(host);

        RoleName administering = await deployment.BeginAsync(
            [Permissions.MembershipManage, Permissions.SystemAdminister],
            cancellationToken);
        RoleName reading = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);
        _ = await elsewhere.BeginAsync([], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(administrator, administrative, cancellationToken);
        await deployment.MemberAsync(member, administrative, cancellationToken);
        await deployment.MemberAsync(member, deployment.Organization, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator),
            administering,
            null,
            false,
            null,
            administrative,
            cancellationToken);

        GrantId stays = await deployment.GrantAsync(
            GrantSubject.Of(member),
            reading,
            null,
            false,
            null,
            deployment.Organization,
            cancellationToken);
        GrantId beyond = await deployment.GrantAsync(
            GrantSubject.Of(member),
            reading,
            null,
            false,
            null,
            elsewhere.Organization,
            cancellationToken);

        Assert.Null(await EndedAsync(administrator, administrative, member));

        Assert.Equal(1, await StandingAsync(stays));
        Assert.Equal(0, await StandingAsync(beyond));
        Assert.Equal(Why, await RevocationReasonAsync(beyond));
    }

    /// <summary>
    /// IDN-MEM-001 AC7 (D-213): an end set in advance is kept on the membership with its
    /// administrator and reason, which stays current, and is recorded with its instant;
    /// cancelled, the row carries none and the record no instant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC7_AnEndSetIsKeptOnTheMembershipAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync([Permissions.MembershipManage], cancellationToken);
        OrganizationId organization = deployment.Organization;
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);
        DateTimeOffset endsAt = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(30);

        await deployment.MemberAsync(administrator, organization, cancellationToken);
        await deployment.MemberAsync(member, organization, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator),
            managing,
            null,
            false,
            null,
            organization,
            cancellationToken);

        SessionId session = await SteppedUpAsync(administrator);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result set = await scope.ServiceProvider.GetRequiredService<IInvitations>()
                .SetMembershipEndAsync(
                    AccessContext.Of(administrator),
                    session,
                    organization,
                    member,
                    endsAt,
                    Why,
                    cancellationToken);

            Assert.True(set.Match(() => true, _ => false));
        }

        (DateTimeOffset? At, Guid? SetBy, string? Reason, DateTimeOffset? EndedAt) kept = await EndSetAsync(member, organization);

        Assert.Equal(endsAt.ToUnixTimeMilliseconds(), kept.At?.ToUnixTimeMilliseconds());
        Assert.Equal((administrator.Value, Why, (DateTimeOffset?)null), (kept.SetBy, kept.Reason, kept.EndedAt));
        Assert.Equal(1, await EndsRecordedAsync(administrator, organization, withInstant: true));

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result cancelled = await scope.ServiceProvider.GetRequiredService<IInvitations>()
                .CancelMembershipEndAsync(
                    AccessContext.Of(administrator),
                    session,
                    organization,
                    member,
                    Why,
                    cancellationToken);

            Assert.True(cancelled.Match(() => true, _ => false));
        }

        Assert.Equal((null, null, null, null), await EndSetAsync(member, organization));
        Assert.Equal(1, await EndsRecordedAsync(administrator, organization, withInstant: false));
    }

    // The end set on the account's membership of the organization, as its row carries it.
    private async Task<(DateTimeOffset? At, Guid? SetBy, string? Reason, DateTimeOffset? EndedAt)> EndSetAsync(
        SubjectId account,
        OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<(DateTimeOffset?, Guid?, string?, DateTimeOffset?)>(new CommandDefinition(
            """
            SELECT ends_at, end_set_by, end_reason, ended_at FROM identity.memberships
            WHERE subject = @account AND organization = @organization
            """,
            new { account = account.Value, organization = organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The ends the administrator set or cancelled in the organization, with the reason, an
    // instant carried or none.
    private async Task<int> EndsRecordedAsync(SubjectId administrator, OrganizationId organization, bool withInstant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'identity.membership.endset' AND acting_subject = @administrator
              AND organization = @organization AND details ->> 'reason' = @reason
              AND (details ->> 'endsAt' IS NOT NULL) = @withInstant
            """,
            new { administrator = administrator.Value, organization = organization.Value, reason = Why, withInstant },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The end of a membership as the administrator asks it, with a step-up of their own:
    // nothing where it succeeded, or the code it was refused with.
    private async Task<ErrorCode?> EndedAsync(SubjectId administrator, OrganizationId organization, SubjectId member)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SessionId session = await SteppedUpAsync(administrator);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result ended = await scope.ServiceProvider.GetRequiredService<IInvitations>()
            .EndMembershipAsync(
                AccessContext.Of(administrator),
                session,
                organization,
                member,
                Why,
                Source,
                cancellationToken);

        return ended.Match(() => (ErrorCode?)null, error => error.Code);
    }

    // The reason the grant's row was revoked with.
    private async Task<string?> RevocationReasonAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT revocation_reason FROM identity.grants WHERE id = @id;",
            new { id = grant.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The grant's rows, revoked or not.
    private async Task<int> RowsAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE id = @id;",
            new { id = grant.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // Whether the group still names the account as a member.
    private async Task<int> GroupsHoldingAsync(SubjectId account, GroupId group)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.group_members
            WHERE group_id = @group AND member_type = 'user' AND member_id = @account
            """,
            new { group = group.Value, account = account.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The records of one action the administrator wrote in the organization, carrying the
    // end's reason.
    private async Task<int> RecordedAsync(string action, SubjectId administrator, OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = @action AND acting_subject = @administrator AND organization = @organization
              AND details ->> 'reason' = @reason
            """,
            new { action, administrator = administrator.Value, organization = organization.Value, reason = Why },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // A session of the account's own that proved a phishing-resistant second factor just
    // now, which the end of a membership asks for as its step-up (AUTH-STEP-001).
    private async Task<SessionId> SteppedUpAsync(SubjectId account)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin(Source, new DeviceDescription("Firefox", "Linux")),
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(account, cancellationToken) is null)
        {
            await keys.CreateAsync(account, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return session.Id;
    }

    // An organization-wide check, as an administrative operation makes it.
    private async Task<bool> HeldAsync(SubjectId account, OrganizationId organization)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(account),
                Permissions.AuditRead,
                organization,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    // The grant's row, read without the library: one that was not revoked.
    private async Task<int> StandingAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE id = @id AND revoked_at IS NULL;",
            new { id = grant.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
