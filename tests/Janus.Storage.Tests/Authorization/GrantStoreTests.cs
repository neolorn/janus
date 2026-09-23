using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Groups;
using Janus.Storage.Authorization.Resources;
using Janus.Storage.Authorization.Roles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// Grants as the table holds them, and the counters a grant change raises
/// (AUTHZ-GRANT-001, AUTHZ-GRANT-003, AUTHZ-CACHE-001, CONV-TEST-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class GrantStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTHZ-GRANT-001 AC3: the same table serves a grant held by an account and one
    /// held by a group, and a read for a principal returns both.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_001_AC3_TheSameTableServesAccountAndGroupGrantsAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        var team = GroupId.New(TimeProvider.System);
        ResourceReference record = Reference("document");

        GrantId held = await WriteAsync(GrantSubject.Of(account), organization, record);
        GrantId shared = await WriteAsync(GrantSubject.Of(team), organization, record);

        IReadOnlyList<Grant> grants = await HeldByAsync(
            [GrantSubject.Of(account), GrantSubject.Of(team)],
            organization);

        Assert.Equal(
            new[] { held, shared }.OrderBy(id => id.Value),
            grants.Select(grant => grant.Id).OrderBy(id => id.Value));
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC2: a row precomputed from a fact reads back as one, so it is
    /// never taken for a row somebody wrote.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC2_AMaterialisedGrantReadsBackAsOneAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        ResourceReference record = Reference("document");

        await RoleAsync();

        Grant materialised = Grant.Create(
            GrantId.New(TimeProvider.System),
            GrantSubject.Of(account),
            RoleName.Parse("editor"),
            organization,
            record,
            deny: false,
            GrantKind.Materialised,
            expiresAt: null,
            await _deployment.AccountAsync(Noon),
            Noon,
            "The derivation this row was precomputed from.")
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);
            await Store(writing).CreateAsync(materialised, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Grant? held = await Store(reading)
            .FindAsync(materialised.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GrantKind.Materialised, held?.Kind);
    }

    /// <summary>
    /// AUTHZ-GRANT-001 AC2: a grant with no resource is on the whole organization, and
    /// is read for every record in it.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganizationAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        ResourceReference record = await RegisterAsync(organization, containedIn: null);

        GrantId wide = await WriteAsync(GrantSubject.Of(account), organization, on: null);

        IReadOnlyList<Grant> grants = await OnAsync(record, organization);

        Assert.Equal([wide], grants.Select(grant => grant.Id));
        Assert.True(grants[0].IsOrganizationWide);
    }

    /// <summary>
    /// AUTHZ-INHERIT-001: a grant on a container is read for everything beneath it, and
    /// a grant on a sibling is not.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_001_AC1_AGrantOnAContainerIsReadForItsContentsAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        ResourceReference workspace = await RegisterAsync(organization, containedIn: null);
        ResourceReference folder = await RegisterAsync(organization, workspace);
        ResourceReference document = await RegisterAsync(organization, folder);
        ResourceReference elsewhere = await RegisterAsync(organization, workspace);

        GrantId above = await WriteAsync(GrantSubject.Of(account), organization, folder);
        await WriteAsync(GrantSubject.Of(account), organization, elsewhere);

        Assert.Equal([above], (await OnAsync(document, organization)).Select(grant => grant.Id));
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC1: a grant past its expiry confers nothing, with no sweep
    /// having run over the table.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC1_AnExpiredGrantIsNotReadWithoutASweepAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        ResourceReference record = Reference("document");

        await WriteAsync(
            GrantSubject.Of(account),
            organization,
            record,
            expiresAt: Noon.AddHours(1));

        Assert.NotEmpty(await HeldByAsync([GrantSubject.Of(account)], organization, Noon));
        Assert.Empty(await HeldByAsync(
            [GrantSubject.Of(account)],
            organization,
            Noon.AddHours(2)));
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC3: who granted a record's grants and when is a query over the
    /// grants themselves, needing no separate log to be kept in step.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC3_WhoGrantedThisAndWhenIsAnsweredByQueryAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId administrator = await _deployment.AccountAsync(Noon);
        ResourceReference record = await RegisterAsync(organization, containedIn: null);

        GrantId id = await WriteAsync(
            GrantSubject.Of(account),
            organization,
            record,
            grantedBy: administrator);

        Grant found = Assert.Single(await OnAsync(record, organization));

        Assert.Equal(id, found.Id);
        Assert.Equal(administrator, found.GrantedBy);
        Assert.Equal(Noon, found.GrantedAt);
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC2: who granted it, when and why are on the row, and so are
    /// who revoked it, when and why.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC2_TheAuditFieldsAreOnTheRowThroughRevocationAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId administrator = await _deployment.AccountAsync(Noon);
        GrantId id = await WriteAsync(
            GrantSubject.Of(account),
            organization,
            Reference("document"),
            grantedBy: administrator);

        Grant written = await ReadAsync(id);

        Assert.Equal(administrator, written.GrantedBy);
        Assert.Equal(Noon, written.GrantedAt);
        Assert.Equal("The reason the grant was written.", written.Reason);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            GrantStore store = Store(writing);
            Grant grant = (await store.FindAsync(id, TestContext.Current.CancellationToken))!;

            Assert.True(grant
                .Revoke(administrator, Noon.AddDays(1), "No longer on the team.")
                .Match(() => true, _ => false));

            await store.RecordAsync(grant, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Grant revoked = await ReadAsync(id);

        Assert.Equal(administrator, revoked.RevokedBy);
        Assert.Equal(Noon.AddDays(1), revoked.RevokedAt);
        Assert.Equal("No longer on the team.", revoked.RevocationReason);
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC1: revoking a grant raises the holder's counter, so the entry
    /// carrying it is orphaned rather than waited out.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC1_RevokingAGrantRaisesTheHoldersCounterAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GrantId id = await WriteAsync(GrantSubject.Of(account), organization, Reference("document"));

        long before = await VersionAsync(account);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            GrantStore store = Store(writing);
            Grant grant = (await store.FindAsync(id, TestContext.Current.CancellationToken))!;

            Assert.True(grant
                .Revoke(account, Noon.AddDays(1), "Taken back.")
                .Match(() => true, _ => false));

            await store.RecordAsync(grant, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await VersionAsync(account) > before);
        Assert.Empty(await HeldByAsync([GrantSubject.Of(account)], organization));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC2: a grant write that rolls back leaves the counter where it
    /// was, so no entry is orphaned by a grant that was never written.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC2_AFailedGrantWriteLeavesTheCounterUnchangedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);

        long before = await VersionAsync(account);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await Store(writing).CreateAsync(
                Written(GrantSubject.Of(account), organization, Reference("document"), null, account),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, await VersionAsync(account));
        Assert.Empty(await HeldByAsync([GrantSubject.Of(account)], organization));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC3: a grant written to a group raises the counter of every
    /// account the group holds, at any depth.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC3_AGroupsGrantRaisesEveryTransitiveMemberAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GroupId department = await GroupAsync(organization, "Department");
        GroupId team = await GroupAsync(organization, "Team");

        await AddAsync(department, GrantSubject.Of(team));
        await AddAsync(team, GrantSubject.Of(account));

        long before = await VersionAsync(account);

        await WriteAsync(GrantSubject.Of(department), organization, Reference("document"));

        Assert.True(await VersionAsync(account) > before);
    }

    /// <summary>
    /// AUTHZ-GRANT-002: a grant saying exactly what a live one already says is a
    /// duplicate, and a revoked one is not.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_AGrantSayingWhatALiveOneSays_ReportsSoAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        ResourceReference record = Reference("document");

        await WriteAsync(GrantSubject.Of(account), organization, record);

        await using StoreContext reading = database.Context();

        Assert.True(await Store(reading).ExistsAsync(
            Written(GrantSubject.Of(account), organization, record, null, account),
            Noon,
            TestContext.Current.CancellationToken));

        Assert.False(await Store(reading).ExistsAsync(
            Written(GrantSubject.Of(account), organization, Reference("document"), null, account),
            Noon,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 and AUTHZ-GRANT-003 AC3: a role any grant confers is named, the
    /// grant revoked or not, and a role no grant confers is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_ARoleAnyGrantConfersIsNamedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId administrator = await _deployment.AccountAsync(Noon);
        GrantId id = await WriteAsync(GrantSubject.Of(account), organization, on: null);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            Grant grant = (await Store(writing).FindAsync(id, TestContext.Current.CancellationToken))!;

            _ = grant.Revoke(administrator, Noon.AddDays(1), "Taken back.");
            await Store(writing).RecordAsync(grant, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.True(await Store(reading).NamesAsync(RoleName.Parse("editor"), TestContext.Current.CancellationToken));
        Assert.False(await Store(reading).NamesAsync(RoleName.Parse("nobody-holds-this"), TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static GrantStore Store(StoreContext context) =>
        new(context, new DataConnections(context));

    private static ResourceReference Reference(string type) =>
        new(ResourceType.Parse(type), ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task<RoleName> RoleAsync()
    {
        var name = RoleName.Parse("editor");

        await using StoreContext writing = database.Context();

        if (await writing.Roles.AnyAsync(row => row.Name == name, TestContext.Current.CancellationToken))
        {
            return name;
        }

        await new RoleStore(writing).CreateAsync(
            Role.Of(name, [Permissions.GrantRead]),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return name;
    }

    private static Grant Written(
        GrantSubject subject,
        OrganizationId organization,
        ResourceReference? on,
        DateTimeOffset? expiresAt,
        SubjectId grantedBy)
    {
        return Grant.Create(
            GrantId.New(TimeProvider.System),
            subject,
            RoleName.Parse("editor"),
            organization,
            on,
            deny: false,
            GrantKind.Stored,
            expiresAt,
            grantedBy,
            Noon,
            "The reason the grant was written.")
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    private async Task<GrantId> WriteAsync(
        GrantSubject subject,
        OrganizationId organization,
        ResourceReference? on,
        DateTimeOffset? expiresAt = null,
        SubjectId? grantedBy = null)
    {
        await RoleAsync();

        Grant grant = Written(
            subject,
            organization,
            on,
            expiresAt,
            grantedBy ?? await _deployment.AccountAsync(Noon));

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await Store(writing).CreateAsync(grant, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return grant.Id;
    }

    private async Task<ResourceReference> RegisterAsync(
        OrganizationId organization,
        ResourceReference? containedIn)
    {
        ResourceReference reference = Reference(containedIn is null ? "workspace" : "document");

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new ResourceStore(writing, new DataConnections(writing)).RegisterAsync(
            RegisteredResource.Create(reference, organization, subject: null, containedIn),
            TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return reference;
    }

    private async Task<GroupId> GroupAsync(OrganizationId organization, string name)
    {
        var id = GroupId.New(TimeProvider.System);

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new GroupStore(writing, new DataConnections(writing)).CreateAsync(
            Group.Create(id, organization, name),
            TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task AddAsync(GroupId group, GrantSubject member)
    {
        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new GroupStore(writing, new DataConnections(writing))
            .AddMemberAsync(group, member, TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long> VersionAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).VersionAsync(subject, TestContext.Current.CancellationToken);
    }

    private async Task<Grant> ReadAsync(GrantId id)
    {
        await using StoreContext reading = database.Context();

        return (await Store(reading).FindAsync(id, TestContext.Current.CancellationToken))!;
    }

    private async Task<IReadOnlyList<Grant>> HeldByAsync(
        IReadOnlyList<GrantSubject> holders,
        OrganizationId organization,
        DateTimeOffset? at = null)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).HeldByAsync(
            holders,
            organization,
            at ?? Noon,
            TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<Grant>> OnAsync(
        ResourceReference reference,
        OrganizationId organization)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).OnAsync(
            reference,
            organization,
            Noon,
            TestContext.Current.CancellationToken);
    }
}
