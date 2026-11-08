using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's check of a removal of access, over the database: a host that keeps the last
/// holder of a role refuses every path that would take it away, one removal at a time, and
/// is not asked where the law, the clock or a deny decides (AUTHZ-GRANT-005, D-281).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
/// <remarks>
/// Each case writes an organization of its own and arms the check for it alone.
/// </remarks>
[Trait("kind", "integration")]
public sealed class AccessRemovalCheckTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Why = "No longer needed.";

    private static readonly ErrorCode LastOwner = ErrorCode.Parse("company.lastowner");

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a revocation through <c>IGrants</c> that would leave the
    /// role no holder is refused with the host's code, writes nothing, and is recorded
    /// as <c>authz.access.removalrefused</c> with its kind, code and path; another holder's
    /// revocation goes ahead while one is left.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ARevocationLeavingTheRoleNoHolderIsRefusedAndWritesNothingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        GrantId first = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);
        GrantId last = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);

        host.Removals.KeepLast(held.Deployment.Organization, held.Kept, LastOwner);

        Assert.Null(Code(await RevokedAsync(held, first)));
        Assert.Equal(LastOwner, Code(await RevokedAsync(held, last)));
        Assert.Equal(0, await StandingAsync(first));
        Assert.Equal(1, await StandingAsync(last));
        Assert.Equal(1, await RecordedAsync(held, "authz.grant.revoked"));
        Assert.Equal(1, await RefusalsRecordedAsync(held, "grantRevoked", LastOwner, "library"));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): two revocations at once of the last two holders of the role
    /// are judged one after the other under the organization's lock, so exactly one goes
    /// ahead and one holder is left.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_TwoRevocationsAtOnceOfTheLastTwoHoldersAdmitOneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        GrantId first = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);
        GrantId second = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);

        host.Removals.KeepLast(held.Deployment.Organization, held.Kept, LastOwner);

        Result[] answered = await Task.WhenAll(
            Task.Run(() => RevokedAsync(held, first), cancellationToken),
            Task.Run(() => RevokedAsync(held, second), cancellationToken));

        Assert.Equal([null, LastOwner], answered.Select(Code).OrderBy(code => code?.ToString(), StringComparer.Ordinal));
        Assert.Equal(1, await StandingAsync(first) + await StandingAsync(second));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a check that throws refuses the revocation with
    /// <c>authz.removal.uncheckable</c>, never letting it through, and writes nothing but
    /// the refusal's record.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ACheckThatThrowsRefusesAndWritesNothingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);

        host.Removals.Arm(held.Deployment.Organization, (_, _) => throw new TimeoutException("The host's store did not answer."));

        Assert.Equal(ErrorCodes.RemovalUncheckable, Code(await RevokedAsync(held, grant)));
        Assert.Equal(1, await StandingAsync(grant));
        Assert.Equal(0, await RecordedAsync(held, "authz.grant.revoked"));
        Assert.Equal(1, await RefusalsRecordedAsync(held, "grantRevoked", ErrorCodes.RemovalUncheckable, "library"));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a code under a library prefix is the host's fault, thrown
    /// to the caller, and the grant stays live.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AHostCodeUnderALibraryPrefixIsAFaultAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(await held.Deployment.AccountAsync(cancellationToken)), held.Kept, null, false, null, null, cancellationToken);

        host.Removals.Arm(
            held.Deployment.Organization,
            (_, _) => ValueTask.FromResult(Result.Failure(Error.From(ErrorCode.Parse("identity.owner.kept")))));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => RevokedAsync(held, grant));
        Assert.Equal(1, await StandingAsync(grant));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): ending a membership now through <c>IInvitations</c> is
    /// asked of the check once, naming the membership with the grants it revokes and the
    /// groups it leaves; refused, the membership stays current, its grant live and its
    /// group kept, and setting an end in advance is refused alike and sets none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_EndingAMembershipNowOrInAdvanceIsCheckedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        OrganizationId organization = held.Deployment.Organization;
        SubjectId member = await held.Deployment.AccountAsync(cancellationToken);
        GroupId group = await held.Deployment.GroupAsync(cancellationToken);

        await held.Deployment.MemberAsync(member, organization, cancellationToken);
        await held.Deployment.JoinAsync(group, GrantSubject.Of(member), cancellationToken);

        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(member), held.Kept, null, false, null, null, cancellationToken);

        host.Removals.KeepLast(organization, held.Kept, LastOwner);

        Assert.Equal(LastOwner, await EndedAsync(held, member));
        Assert.Equal(LastOwner, await EndSetAsync(held, member, Deployment.Noon.AddDays(30)));

        AccessRemoval asked = host.Removals.AskedIn(organization)[0];

        Assert.Equal(
            (AccessRemovalKind.MembershipEnded, GrantSubject.Of(member), AccessRemovalPath.Library, Why),
            (asked.Kind, asked.Subject, asked.Path, asked.Reason));
        Assert.Equal([grant], asked.Grants.Select(removed => removed.Grant));
        Assert.Equal([group], asked.Groups);
        Assert.Equal(2, host.Removals.AskedIn(organization).Count);
        Assert.Equal((true, (DateTimeOffset?)null), await MembershipAsync(member, organization));
        Assert.Equal(1, await StandingAsync(grant));
        Assert.Equal(1, await MembersAsync(group, member));
        Assert.Equal(2, await RefusalsRecordedAsync(held, "membershipEnded", LastOwner, "library"));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): the job <c>membership-ends</c> asks the check again when it
    /// carries an end out; refused, the membership stays and its end stays set, and the
    /// next pass, once the check admits it, ends it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AScheduledEndTheCheckRefusesKeepsTheMembershipAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        OrganizationId organization = held.Deployment.Organization;
        SubjectId member = await held.Deployment.AccountAsync(cancellationToken);
        DateTimeOffset endsAt = Deployment.Noon.AddDays(30);

        await held.Deployment.MemberAsync(member, organization, cancellationToken);

        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(member), held.Kept, null, false, null, null, cancellationToken);

        Assert.Null(await EndSetAsync(held, member, endsAt));

        host.Removals.KeepLast(organization, held.Kept, LastOwner);

        await PassAsync(endsAt.AddSeconds(1));

        Assert.Equal((true, (DateTimeOffset?)endsAt), await MembershipAsync(member, organization));
        Assert.Equal(1, await StandingAsync(grant));
        Assert.Equal(AccessRemovalPath.ScheduledEnd, host.Removals.AskedIn(organization)[^1].Path);
        Assert.Equal(1, await RefusalsRecordedAsync(held, "membershipEnded", LastOwner, "scheduledEnd"));

        host.Removals.Arm(organization, (_, _) => ValueTask.FromResult(Result.Success()));

        await PassAsync(endsAt.AddSeconds(2));

        Assert.False((await MembershipAsync(member, organization)).Current);
        Assert.Equal(0, await StandingAsync(grant));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-240, D-281): a member bringing their own set end forward is asked
    /// of the check as an end now is, and refused, stays a member.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AnEndBroughtForwardIsCheckedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        OrganizationId organization = held.Deployment.Organization;
        SubjectId member = await held.Deployment.AccountAsync(cancellationToken);
        DateTimeOffset endsAt = Deployment.Noon.AddDays(30);

        await held.Deployment.MemberAsync(member, organization, cancellationToken);
        _ = await held.Deployment.GrantAsync(GrantSubject.Of(member), held.Kept, null, false, null, null, cancellationToken);

        Assert.Null(await EndSetAsync(held, member, endsAt));

        host.Removals.KeepLast(organization, held.Kept, LastOwner);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result brought = await scope.ServiceProvider.GetRequiredService<IInvitations>()
            .BringMembershipEndForwardAsync(AccessContext.Of(member), organization, cancellationToken);

        Assert.Equal(LastOwner, Code(brought));
        Assert.Equal((true, (DateTimeOffset?)endsAt), await MembershipAsync(member, organization));
        Assert.Equal(MembershipEnd.BroughtForward, host.Removals.AskedIn(organization)[^1].Reason);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): taking a member out of a group that reaches a grant is
    /// asked of the check, and refused, the member stays; a group reaching only a deny is
    /// not asked about.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AGroupMemberRemovalReachingAGrantIsCheckedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        OrganizationId organization = held.Deployment.Organization;
        SubjectId member = await held.Deployment.AccountAsync(cancellationToken);
        GroupId reaching = await held.Deployment.GroupAsync(cancellationToken);
        GroupId denying = await held.Deployment.GroupAsync(cancellationToken);

        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(reaching), held.Kept, null, false, null, null, cancellationToken);
        _ = await held.Deployment.GrantAsync(GrantSubject.Of(denying), held.Kept, null, true, null, null, cancellationToken);
        await held.Deployment.JoinAsync(reaching, GrantSubject.Of(member), cancellationToken);
        await held.Deployment.JoinAsync(denying, GrantSubject.Of(member), cancellationToken);

        host.Removals.Arm(
            organization,
            (_, _) => ValueTask.FromResult(Result.Failure(Error.From(LastOwner))));

        Assert.Equal(LastOwner, Code(await RemovedAsync(held, reaching, member)));
        Assert.Null(Code(await RemovedAsync(held, denying, member)));

        AccessRemoval asked = Assert.Single(host.Removals.AskedIn(organization));

        Assert.Equal(
            (AccessRemovalKind.GroupMemberRemoved, (GroupId?)reaching, GrantSubject.Of(member)),
            (asked.Kind, asked.Group, asked.Subject));
        Assert.Equal([grant], asked.Grants.Select(removed => removed.Grant));
        Assert.Equal(1, await MembersAsync(reaching, member));
        Assert.Equal(0, await MembersAsync(denying, member));
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): an account's erasure revokes its grants whatever the host's
    /// check would say, and a grant past its end confers nothing, neither asked of it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ExpiryAndErasureAreNotCheckedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case held = await CaseAsync();
        OrganizationId organization = held.Deployment.Organization;
        SubjectId erased = await held.Deployment.AccountAsync(cancellationToken);
        SubjectId expiring = await held.Deployment.AccountAsync(cancellationToken);
        GrantId grant = await held.Deployment.GrantAsync(
            GrantSubject.Of(erased), held.Kept, null, false, null, null, cancellationToken);
        GrantId expired = await held.Deployment.GrantAsync(
            GrantSubject.Of(expiring), held.Kept, null, false, Deployment.Noon.AddMinutes(-1), null, cancellationToken);

        host.Removals.Arm(
            organization,
            (_, _) => ValueTask.FromResult(Result.Failure(Error.From(LastOwner))));

        // PRIV-RIGHT-005: an account is erased from the deletion its owner asked for.
        await using (NpgsqlConnection connection = await host.OpenAsync())
        {
            _ = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE identity.accounts SET state = 'deleting', deleting_by = 'self', deleting_since = @at WHERE subject = @subject;",
                new { subject = erased.Value, at = Deployment.Noon },
                cancellationToken: cancellationToken));
        }

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            _ = await work.BeginAsync(cancellationToken);
            _ = await scope.ServiceProvider.GetRequiredService<ISubjectEraser>()
                .EraseAsync(erased, ErasureReason.ErasureRequest, Deployment.Noon, cancellationToken);
            _ = await work.CommitAsync(cancellationToken);
        }

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result admitted = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(expiring), HostPermissions.Read, organization, cancellationToken);

            Assert.False(admitted.Match(() => true, _ => false));
        }

        Assert.Equal(0, await StandingAsync(grant));
        Assert.Equal(1, await StandingAsync(expired));
        Assert.Empty(host.Removals.AskedIn(organization));
    }

    private static ErrorCode? Code(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

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

        _ = await work.BeginAsync(cancellationToken);

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
        _ = await work.CommitAsync(cancellationToken);

        return session.Id;
    }

    // An organization whose administrator manages grants, groups and memberships through
    // a role handing out the role the host keeps a holder of.
    private async Task<Case> CaseAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName kept = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName managing = await deployment.RoleAsync(
            [Permissions.GrantManage, Permissions.GrantRead, Permissions.GroupManage, Permissions.MembershipManage],
            cancellationToken);
        SubjectId actor = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(actor, deployment.Organization, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(actor), managing, null, false, null, null, cancellationToken);
        await deployment.HandsOutAsync(managing, [kept], cancellationToken);

        return new Case(deployment, actor, kept);
    }

    private async Task<Result> RevokedAsync(Case held, GrantId grant)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return await scope.ServiceProvider.GetRequiredService<IGrants>().RevokeAsync(
            AccessContext.Of(held.Actor),
            session,
            grant,
            Why,
            TestContext.Current.CancellationToken);
    }

    private async Task<Result> RemovedAsync(Case held, GroupId group, SubjectId member)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return await scope.ServiceProvider.GetRequiredService<IGroups>().RemoveMemberAsync(
            AccessContext.Of(held.Actor),
            session,
            group,
            GrantSubject.Of(member),
            Why,
            TestContext.Current.CancellationToken);
    }

    private async Task<ErrorCode?> EndedAsync(Case held, SubjectId member)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return Code(await scope.ServiceProvider.GetRequiredService<IInvitations>().EndMembershipAsync(
            AccessContext.Of(held.Actor),
            session,
            held.Deployment.Organization,
            member,
            Why,
            "198.51.100.7",
            TestContext.Current.CancellationToken));
    }

    private async Task<ErrorCode?> EndSetAsync(Case held, SubjectId member, DateTimeOffset endsAt)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SessionId session = await SteppedUpAsync(scope.ServiceProvider, held.Actor);

        return Code(await scope.ServiceProvider.GetRequiredService<IInvitations>().SetMembershipEndAsync(
            AccessContext.Of(held.Actor),
            session,
            held.Deployment.Organization,
            member,
            endsAt,
            Why,
            TestContext.Current.CancellationToken));
    }

    // One pass of the job membership-ends, run at the instant given.
    private async Task PassAsync(DateTimeOffset at)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        MembershipEnd ends = ActivatorUtilities.CreateInstance<MembershipEnd>(scope.ServiceProvider, new FixedTime(at));
        Result<int> pass = await ends.EndDueAsync(
            AccessContext.Of(SystemPrincipal.ForDeployment("membership-ends", "IDN-MEM-001", SystemOperation.ExpirySweep)),
            TestContext.Current.CancellationToken);

        Assert.True(pass.Match(_ => true, _ => false));
    }

    // Whether the grant's row is still unrevoked.
    private async Task<int> StandingAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE id = @id AND revoked_at IS NULL;",
            new { id = grant.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // Whether the account's membership is current, and the end set on it.
    private async Task<(bool Current, DateTimeOffset? EndsAt)> MembershipAsync(SubjectId account, OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<(bool, DateTimeOffset?)>(new CommandDefinition(
            """
            SELECT ended_at IS NULL, ends_at FROM identity.memberships
            WHERE subject = @account AND organization = @organization
            """,
            new { account = account.Value, organization = organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<int> MembersAsync(GroupId group, SubjectId member)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.group_members WHERE group_id = @group AND member_id = @member;",
            new { group = group.Value, member = member.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The records of one action filed under the case's organization.
    private async Task<int> RecordedAsync(Case held, string action)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.audit_records WHERE action = @action AND organization = @organization;",
            new { action, organization = held.Deployment.Organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The refusals of the host's check recorded under the case's organization, with the
    // kind, the code, the path and the outcome they carry.
    private async Task<int> RefusalsRecordedAsync(Case held, string kind, ErrorCode code, string path)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.removalrefused' AND organization = @organization
              AND category = 'security'
              AND details ->> 'kind' = @kind AND details ->> 'code' = @code
              AND details ->> 'path' = @path AND details ->> 'outcome' = 'refused'
            """,
            new { organization = held.Deployment.Organization.Value, kind, code = code.ToString(), path },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // One case's deployment, its administrator and the role the host keeps a holder of.
    private sealed record Case(Deployment Deployment, SubjectId Actor, RoleName Kept);
}
