using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A person reads the live grants their own account holds, with no permission, as the
/// subject export carries them (AUTHZ-GRANT-003, D-274).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class OwnGrantsTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// AUTHZ-GRANT-003 AC9 (D-274): a person holding no <c>grant:read</c> is answered the
    /// live grants their own account holds in its own name there, oldest first, and
    /// neither an expired nor a revoked one, another account's, nor a group's it belongs
    /// to; asking for their own grants under <c>grant:read</c> is refused, and the
    /// self-read records nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC9_APersonReadsTheirOwnLiveGrantsAloneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();
        Result<IReadOnlyList<OwnGrant>> own = await grants.OwnAsync(
            AccessContext.Of(written.Account),
            written.Deployment.Organization,
            cancellationToken);

        Assert.Equal(written.Live, own.Match(held => held.Select(grant => grant.Id), _ => []));

        OwnGrant onRecord = own.Match(held => held[0], error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(written.Record, onRecord.On);
        Assert.Equal(written.Role, onRecord.Role);
        Assert.Equal(GrantKind.Stored, onRecord.Kind);
        Assert.False(onRecord.Deny);
        Assert.Null(onRecord.ExpiresAt);
        Assert.Equal(Deployment.Noon, onRecord.GrantedAt);
        Assert.Equal(0, await DenialsAsync(written.Account));

        Result<IReadOnlyList<HeldGrant>> held = await grants.HeldAsync(
            AccessContext.Of(written.Account),
            written.Deployment.Organization,
            GrantSubject.Of(written.Account),
            cancellationToken);

        Assert.Equal(ErrorCodes.Denied, held.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(1, await DenialsAsync(written.Account));
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC9, IDN-ACCT-007 (D-274): a restricted account is answered its
    /// own grants; one holding none in an organization, or asking of an organization the
    /// deployment does not hold, is answered none; a principal, which has no account of
    /// its own, is refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC9_ARestrictedAccountIsAnsweredItsOwnAndAPrincipalNoneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync();

        await written.Deployment.RestrictAsync(written.Account, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();

        async Task<IEnumerable<GrantId>> OwnAsync(AccessContext context, OrganizationId organization) =>
            (await grants.OwnAsync(context, organization, cancellationToken))
                .Match(held => held.Select(grant => grant.Id), error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(written.Live, await OwnAsync(AccessContext.Of(written.Account), written.Deployment.Organization));
        Assert.Empty(await OwnAsync(
            AccessContext.Of(await written.Deployment.AccountAsync(cancellationToken)),
            written.Deployment.Organization));
        Assert.Empty(await OwnAsync(AccessContext.Of(written.Account), new OrganizationId(Guid.NewGuid())));

        Result<IReadOnlyList<OwnGrant>> principal = await grants.OwnAsync(
            AccessContext.Of(SystemPrincipal.ForOrganization(
                HostFixture.Digest,
                "the nightly digest",
                written.Deployment.Organization)),
            written.Deployment.Organization,
            cancellationToken);

        Assert.Equal(ErrorCodes.Denied, principal.Match<ErrorCode?>(_ => null, error => error.Code));
    }

    // One account holding, in its own name, a grant on a record and an organization-wide
    // deny written after it, besides an expired grant, a revoked one and a grant of a
    // group it belongs to; and another account holding a grant of its own.
    private async Task<Written> WrittenAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        SubjectId other = await deployment.AccountAsync(cancellationToken);
        GroupId group = await deployment.GroupAsync(cancellationToken);
        var workspace = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));
        var record = new ResourceReference(Document, ResourceId.Parse(Guid.NewGuid().ToString()));

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.MemberAsync(account, deployment.Organization, cancellationToken);
        await deployment.JoinAsync(group, GrantSubject.Of(account), cancellationToken);

        GrantId onRecord = await deployment.GrantAsync(
            GrantSubject.Of(account), role, record, false, null, null, cancellationToken);
        GrantId wide = await deployment.GrantAsync(
            GrantSubject.Of(account), role, null, true, null, null, cancellationToken);
        GrantId revoked = await deployment.GrantAsync(
            GrantSubject.Of(account), role, workspace, false, null, null, cancellationToken);

        _ = await deployment.GrantAsync(
            GrantSubject.Of(account), role, workspace, false, Deployment.Noon.AddDays(-1), null, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(group), role, workspace, false, null, null, cancellationToken);
        _ = await deployment.GrantAsync(GrantSubject.Of(other), role, record, false, null, null, cancellationToken);

        await deployment.RevokeAsync(revoked, cancellationToken);

        // Both live grants were written at one instant; the one on the record is written
        // first in time, so the answer's order is stated.
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE identity.grants SET granted_at = @at WHERE id = @id;",
            new { at = Deployment.Noon.AddMinutes(1), id = wide.Value },
            cancellationToken: cancellationToken));

        return new Written(deployment, account, record, role, [onRecord, wide]);
    }

    // The refusals recorded naming the account as acting.
    private async Task<int> DenialsAsync(SubjectId account)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND acting_subject = @account;
            """,
            new { account = account.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // One case's rows: the account, the record its first grant is on, the role, and its
    // live grants, oldest first.
    private sealed record Written(
        Deployment Deployment,
        SubjectId Account,
        ResourceReference Record,
        RoleName Role,
        IReadOnlyList<GrantId> Live);
}
