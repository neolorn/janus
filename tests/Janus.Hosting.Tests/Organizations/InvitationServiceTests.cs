using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
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

    /// <summary>
    /// IDN-MEM-001, IDN-LIFE-009a, D-166: ending an account's membership of the
    /// administrative organization stops what its grants there confer, and removes no
    /// grant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_EndingTheAdministrativeMembershipStopsItsGrantsAndKeepsThemAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync([Permissions.MembershipManage], cancellationToken);
        RoleName reading = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

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

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result ended = await scope.ServiceProvider.GetRequiredService<IInvitations>()
                .EndMembershipAsync(
                    AccessContext.Of(administrator),
                    administrative,
                    member,
                    Source,
                    cancellationToken);

            Assert.True(ended.Match(() => true, _ => false));
        }

        Assert.False(await HeldAsync(member, administrative));
        Assert.Equal(1, await StandingAsync(held));
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
