using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Identity.Audit;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What a grant written or revoked leaves in the trail (AUTHZ-GRANT-003, IDN-AUD-001).
/// </summary>
/// <remarks>
/// The port implementation is tested against the real database (D-156). One database
/// serves the class, so each test writes with an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class GrantAuditTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly RoleName Clerk = RoleName.Parse("clerk");

    private static readonly SystemPrincipal Bootstrap =
        SystemPrincipal.ForDeployment("bootstrap", "OPS-BOOT-001", SystemOperation.Bootstrap);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// AUTHZ-GRANT-003 AC6: a grant a person writes to an account is recorded as
    /// <c>authz.grant.created</c> naming the person as actor and the holder as subject,
    /// with the grant, its role and the reason, in the grant's organization.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC6_AGrantToAnAccountIsRecordedCreatedNamingItsHolderAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);
        SubjectId holder = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        Grant grant = Granted(GrantSubject.Of(holder), organization, actor, now);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).CreatedAsync(grant, actor, breakGlassReason: null, TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.GrantCreated, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(actor, read.ActingSubject);
        Assert.Equal(holder, read.Subject);
        Assert.Equal(organization, read.Organization);
        Assert.Equal(grant.Id.ToString(), read.Details["grant"].GetString());
        Assert.Equal("clerk", read.Details["role"].GetString());
        Assert.Equal("Counter staff.", read.Details["reason"].GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC6: a grant to a group, created and revoked, is recorded with no
    /// subject, whoever acted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC6_AGrantToAGroupIsRecordedWithNoSubjectAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        Grant grant = Granted(GrantSubject.Of(GroupId.New(TimeProvider.System)), organization, actor, now);

        grant.Revoke(actor, now, "Left the project.")
            .Switch(() => { }, error => Assert.Fail(error.Code.ToString()));

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).CreatedAsync(grant, actor, breakGlassReason: null, TestContext.Current.CancellationToken);
            await Audit(writing).RevokedAsync(grant, breakGlassReason: null, TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IReadOnlyList<AuditRecord> read = await RecordsAsync(actor);

        Assert.Equal(
            ["authz.grant.created", "authz.grant.revoked"],
            read.Select(record => record.Action.ToString()).Order(StringComparer.Ordinal));
        Assert.All(read, record => Assert.Null(record.Subject));
        Assert.All(read, record => Assert.Equal(actor, record.ActingSubject));
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC6: a revocation a person makes in the break-glass session is
    /// recorded as <c>authz.grant.revoked</c> naming the holder as subject, the person
    /// as actor, the revocation's reason and the reason given for the break-glass use.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC6_ARevocationIsRecordedNamingTheHolderWhoeverRevokedItAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);
        SubjectId holder = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        Grant grant = Granted(GrantSubject.Of(holder), organization, actor, now);

        grant.Revoke(actor, now, "Left the project.")
            .Switch(() => { }, error => Assert.Fail(error.Code.ToString()));

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).RevokedAsync(grant, "Outage.", TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.GrantRevoked, read.Action);
        Assert.Equal(actor, read.ActingSubject);
        Assert.Equal(holder, read.Subject);
        Assert.Equal("Outage.", read.BreakGlassReason);
        Assert.Equal("Left the project.", read.Details["reason"].GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC6: a grant bootstrap writes is recorded as
    /// <c>authz.grant.created</c> under bootstrap's principal, naming the holder as
    /// subject and the reason <c>OPS-BOOT-001</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC6_AGrantBootstrapWritesIsRecordedUnderItsPrincipalAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId holder = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        Grant grant = Granted(GrantSubject.Of(holder), organization, default, now, "OPS-BOOT-001");

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).CreatedAsync(grant, Bootstrap, TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(holder));

        Assert.Equal(AuditActions.GrantCreated, read.Action);
        Assert.Equal(holder, read.Subject);
        Assert.Equal("bootstrap", read.Principal);
        Assert.Equal("OPS-BOOT-001", read.Reason);
        Assert.Equal("OPS-BOOT-001", read.Details["reason"].GetString());
    }

    private static Grant Granted(
        GrantSubject holder,
        OrganizationId organization,
        SubjectId grantedBy,
        DateTimeOffset at,
        string reason = "Counter staff.") =>
        Grant
            .Create(
                GrantId.New(TimeProvider.System),
                holder,
                Clerk,
                organization,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                grantedBy,
                at,
                reason)
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

    private GrantAudit Audit(StoreContext context) =>
        new(new AuditStore(context, new DataConnections(context), _deployment.Ring, _deployment.Randomness), TimeProvider.System);

    private async Task<IReadOnlyList<AuditRecord>> RecordsAsync(SubjectId named)
    {
        await using StoreContext reading = database.Context();

        return await new AuditStore(reading, new DataConnections(reading), _deployment.Ring, _deployment.Randomness)
            .FindNamingAsync(named, TestContext.Current.CancellationToken);
    }
}
