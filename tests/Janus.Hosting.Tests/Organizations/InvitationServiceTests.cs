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

        SessionId session = await SteppedUpAsync(administrator);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result ended = await scope.ServiceProvider.GetRequiredService<IInvitations>()
                .EndMembershipAsync(
                    AccessContext.Of(administrator),
                    session,
                    administrative,
                    member,
                    Source,
                    cancellationToken);

            Assert.True(ended.Match(() => true, _ => false));
        }

        Assert.False(await HeldAsync(member, administrative));
        Assert.Equal(1, await StandingAsync(held));
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
