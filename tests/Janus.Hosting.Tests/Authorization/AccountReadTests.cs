using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// How far a host's read of another person's account reaches, read against the
/// deployment's own gate and store: the organization the read is asked in, the people
/// tied to it, a declared job's reads, and the read volume they count toward
/// (IDN-READ-003, IDN-READ-004, D-218).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class AccountReadTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    /// <summary>
    /// IDN-READ-004 AC1 (D-218): a holder of <c>contact:read</c> in an organization reads
    /// the contact details of a member and of the data subject of a record registered in
    /// it, and is answered <c>identity.account.notfound</c> for an account tied to another
    /// organization alone, exactly as for a subject no account bears.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AC1_AnOrganizationReadsThePeopleTiedToItAloneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        var elsewhere = new Deployment(host);

        RoleName reading = await deployment.BeginAsync([Permissions.ContactRead], cancellationToken);
        _ = await elsewhere.BeginAsync([], cancellationToken);

        SubjectId clerk = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);
        SubjectId customer = await deployment.AccountAsync(cancellationToken);
        SubjectId stranger = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(member, deployment.Organization, cancellationToken);
        await deployment.RegisterAsync(Reference(), containedIn: null, cancellationToken, customer);
        await elsewhere.MemberAsync(stranger, elsewhere.Organization, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(clerk), reading, null, false, null, null, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccounts accounts = scope.ServiceProvider.GetRequiredService<IAccounts>();
        var asking = AccessContext.Of(clerk);

        Assert.Equal(
            AccountState.Active,
            Read(await accounts.ContactAsync(asking, member, deployment.Organization, cancellationToken)).State);
        Assert.Equal(
            AccountState.Active,
            Read(await accounts.ContactAsync(asking, customer, deployment.Organization, cancellationToken)).State);

        Error beyond = Refusal(
            await accounts.ContactAsync(asking, stranger, deployment.Organization, cancellationToken));
        Error nobody = Refusal(await accounts.ContactAsync(
            asking, new SubjectId(Guid.NewGuid()), deployment.Organization, cancellationToken));

        Assert.Equal(ErrorCodes.AccountNotFound, beyond.Code);
        Assert.Equal(nobody.Code, beyond.Code);
        Assert.Equal(nobody.Details, beyond.Details);
    }

    /// <summary>
    /// IDN-READ-004 AC2 (D-218): a holder of the reads in the administrative organization,
    /// asking where no organization is named, reads an account tied to no organization of
    /// theirs, and finds it by its identifier.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AC2_AnAdministrativeHolderReadsAnyAccountAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName reading = await deployment.RoleAsync(
            [Permissions.AccountRead, Permissions.ContactRead], cancellationToken);
        SubjectId steward = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(member, deployment.Organization, cancellationToken);
        await deployment.MemberAsync(steward, administrative, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(steward), reading, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccounts accounts = scope.ServiceProvider.GetRequiredService<IAccounts>();
        var asking = AccessContext.Of(steward);

        AccountView account = Read(await accounts.ReadAsync(asking, member, null, cancellationToken));

        Assert.Equal(member, account.Subject);
        Assert.Equal([deployment.Organization], account.Memberships);
        Assert.Equal(
            AccountState.Active,
            Read(await accounts.ContactAsync(asking, member, null, cancellationToken)).State);
    }

    /// <summary>
    /// IDN-READ-004 AC3 (D-218): a job declaring <c>contact:read</c> reads the contact
    /// details of the data subject of its organization's record outside a request, and is
    /// refused <c>authz.denied</c> while a request is served.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AC3_AJobReadsContactDetailsOutsideARequestAloneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        SubjectId customer = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(Reference(), containedIn: null, cancellationToken, customer);

        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            deployment.Organization));
        Result<ContactDetail>? inRequest = null;

        await using (ServedRequests served = await ServedRequests.StartAsync(
            host,
            async context => inRequest = await context.RequestServices
                .GetRequiredService<IAccounts>()
                .ContactAsync(job, customer, deployment.Organization, context.RequestAborted),
            cancellationToken))
        {
            _ = await served.SendAsync(ServedRequests.Browser);
        }

        await using AsyncServiceScope background = host.Services.CreateAsyncScope();
        IAccounts accounts = background.ServiceProvider.GetRequiredService<IAccounts>();

        Assert.Equal(
            AccountState.Active,
            Read(await accounts.ContactAsync(job, customer, deployment.Organization, cancellationToken)).State);
        Assert.Equal(ErrorCodes.Denied, Refusal(inRequest ?? throw new InvalidOperationException("No request.")).Code);
    }

    /// <summary>
    /// IDN-READ-004 AC4 (D-218): each account and contact a person reads adds one record
    /// to their read volume, and a declared job's read adds nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AC4_APersonsReadsAddToTheirReadVolumeAndAJobsDoNotAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName reading = await deployment.BeginAsync(
            [Permissions.AccountRead, Permissions.ContactRead], cancellationToken);

        SubjectId clerk = await deployment.AccountAsync(cancellationToken);
        SubjectId customer = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(Reference(), containedIn: null, cancellationToken, customer);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(clerk), reading, null, false, null, null, cancellationToken);

        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            deployment.Organization));

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccounts accounts = scope.ServiceProvider.GetRequiredService<IAccounts>();

        long before = await AllVolumeAsync(cancellationToken);

        _ = Read(await accounts.ReadAsync(AccessContext.Of(clerk), customer, deployment.Organization, cancellationToken));
        _ = Read(await accounts.ContactAsync(AccessContext.Of(clerk), customer, deployment.Organization, cancellationToken));
        _ = Read(await accounts.ContactAsync(job, customer, deployment.Organization, cancellationToken));

        Assert.Equal(2, await VolumeAsync(clerk, cancellationToken));
        Assert.Equal(before + 2, await AllVolumeAsync(cancellationToken));
    }

    /// <summary>
    /// IDN-READ-004 AC1 and AC4 (D-218): a holder of <c>consent:read</c> in an
    /// organization reads the standing of a member and of the data subject of a record
    /// registered in it, each counted toward their read volume, and is answered
    /// <c>identity.account.notfound</c> for an account tied to another organization alone,
    /// exactly as for a subject no account bears.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AC1_AStandingReachesThePeopleTiedToTheOrganizationAloneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        var elsewhere = new Deployment(host);

        RoleName reading = await deployment.BeginAsync([Permissions.ConsentRead], cancellationToken);
        _ = await elsewhere.BeginAsync([], cancellationToken);

        SubjectId clerk = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);
        SubjectId customer = await deployment.AccountAsync(cancellationToken);
        SubjectId stranger = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(member, deployment.Organization, cancellationToken);
        await deployment.RegisterAsync(Reference(), containedIn: null, cancellationToken, customer);
        await elsewhere.MemberAsync(stranger, elsewhere.Organization, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(clerk), reading, null, false, null, null, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IConsents consents = scope.ServiceProvider.GetRequiredService<IConsents>();
        var asking = AccessContext.Of(clerk);

        Assert.Equal(
            AccountState.Active,
            Read(await consents.StandingAsync(asking, member, deployment.Organization, cancellationToken)).State);
        Assert.Equal(
            AccountState.Active,
            Read(await consents.StandingAsync(asking, customer, deployment.Organization, cancellationToken)).State);

        Error beyond = Refusal(
            await consents.StandingAsync(asking, stranger, deployment.Organization, cancellationToken));
        Error nobody = Refusal(await consents.StandingAsync(
            asking, new SubjectId(Guid.NewGuid()), deployment.Organization, cancellationToken));

        Assert.Equal(ErrorCodes.AccountNotFound, beyond.Code);
        Assert.Equal(nobody.Code, beyond.Code);
        Assert.Equal(nobody.Details, beyond.Details);
        Assert.Equal(2, await VolumeAsync(clerk, cancellationToken));
    }

    /// <summary>
    /// IDN-READ-003 AC5 (D-218): an administrator's setting of another person's
    /// preferences is written to the trail as <c>identity.preferences.changed</c>, the
    /// administrator acting, the person the subject and the reason in
    /// <c>details.reason</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_003_AC5_AnAdministratorsSettingIsRecordedWithTheReasonAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.AccountManage], cancellationToken);
        SubjectId steward = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(steward, administrative, cancellationToken);
        _ = await deployment.GrantAsync(
            GrantSubject.Of(steward), managing, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccounts accounts = scope.ServiceProvider.GetRequiredService<IAccounts>();

        Result set = await accounts.SetPreferencesAsync(
            AccessContext.Of(steward),
            member,
            "ar",
            timeZone: null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "Ticket 4471",
            cancellationToken);

        Assert.Null(set.Match<ErrorCode?>(() => null, error => error.Code));

        await using NpgsqlConnection connection = await host.OpenAsync();

        string? reason = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT details->>'reason' FROM identity.audit_records
            WHERE action = 'identity.preferences.changed' AND acting_subject = @steward AND subject = @member
            """,
            new { steward = steward.Value, member = member.Value },
            cancellationToken: cancellationToken));

        Assert.Equal("Ticket 4471", reason);
    }

    private static ResourceReference Reference() => new(Document, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static TValue Read<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static Error Refusal<TValue>(Result<TValue> result) =>
        result.Match(_ => throw new InvalidOperationException("It was answered."), error => error);

    private async Task<long> VolumeAsync(SubjectId actor, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COALESCE(SUM(records), 0) FROM identity.read_volume WHERE actor = @actor;",
            new { actor = actor.Value },
            cancellationToken: cancellationToken));
    }

    // Every count the deployment holds, so a read counted for no person would show.
    private async Task<long> AllVolumeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COALESCE(SUM(records), 0) FROM identity.read_volume;",
            cancellationToken: cancellationToken));
    }
}
