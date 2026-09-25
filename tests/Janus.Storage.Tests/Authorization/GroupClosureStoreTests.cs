using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authorization.Groups;
using Janus.Core;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Groups;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// The groups of an organization, the group closure as a membership change leaves it,
/// and the counters the change raises (AUTHZ-GROUP-001, AUTHZ-CACHE-001,
/// CONV-TEST-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class GroupClosureStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTHZ-GROUP-001 AC1: a user in a team inside a department belongs to the
    /// department, so the department's grants are read for the user.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GROUP_001_AC1_AUserInATeamInsideADepartmentBelongsToBothAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GroupId department = await GroupAsync(organization, "Department");
        GroupId team = await GroupAsync(organization, "Team");

        await AddAsync(department, GrantSubject.Of(team));
        await AddAsync(team, GrantSubject.Of(account));

        Assert.Equal(
            new[] { department, team }.OrderBy(group => group.Value),
            await GroupsOfAsync(GrantSubject.Of(account)));
    }

    /// <summary>
    /// AUTHZ-GROUP-001 AC2: nesting depth is not fixed by the schema, so a chain far
    /// deeper than any structure a host would build still resolves in one read.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GROUP_001_AC2_NestingDepthIsNotFixedBySchemaAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        List<GroupId> chain = [];

        for (int depth = 0; depth < 32; depth++)
        {
            chain.Add(await GroupAsync(
                organization,
                "Level " + depth.ToString(CultureInfo.InvariantCulture)));
        }

        for (int depth = 1; depth < chain.Count; depth++)
        {
            await AddAsync(chain[depth - 1], GrantSubject.Of(chain[depth]));
        }

        await AddAsync(chain[^1], GrantSubject.Of(account));

        Assert.Equal(
            chain.OrderBy(group => group.Value),
            await GroupsOfAsync(GrantSubject.Of(account)));
    }

    /// <summary>
    /// AUTHZ-GROUP-001 AC1: taking a nested group out of its parent takes every account
    /// beneath it out of the parent with it.
    /// </summary>
    [Fact]
    public async Task RemoveMemberAsync_NestedGroup_TakesItsAccountsOutWithItAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GroupId department = await GroupAsync(organization, "Department");
        GroupId team = await GroupAsync(organization, "Team");

        await AddAsync(department, GrantSubject.Of(team));
        await AddAsync(team, GrantSubject.Of(account));
        await RemoveAsync(department, GrantSubject.Of(team));

        Assert.Equal([team], await GroupsOfAsync(GrantSubject.Of(account)));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC3: a membership change raises the counter of every account it
    /// reaches, at any depth, in the same transaction.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC3_AMembershipChangeRaisesEveryTransitiveMemberAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        GroupId department = await GroupAsync(organization, "Department");
        GroupId team = await GroupAsync(organization, "Team");

        await AddAsync(team, GrantSubject.Of(account));
        await AddAsync(team, GrantSubject.Of(other));

        long heldByAccount = await VersionAsync(account);
        long heldByOther = await VersionAsync(other);

        await AddAsync(department, GrantSubject.Of(team));

        Assert.True(await VersionAsync(account) > heldByAccount);
        Assert.True(await VersionAsync(other) > heldByOther);
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC3: an account taken out of a group has its counter raised by
    /// the removal, so the entry holding the group's grants is orphaned.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC3_LeavingAGroupRaisesTheLeaversCounterAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GroupId team = await GroupAsync(organization, "Team");

        await AddAsync(team, GrantSubject.Of(account));

        long before = await VersionAsync(account);

        await RemoveAsync(team, GrantSubject.Of(account));

        Assert.True(await VersionAsync(account) > before);
        Assert.Empty(await GroupsOfAsync(GrantSubject.Of(account)));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC2: a membership change that rolls back leaves neither the
    /// closure nor the counter moved.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC2_ARolledBackMembershipChangeLeavesTheCounterAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        GroupId team = await GroupAsync(organization, "Team");

        long before = await VersionAsync(account);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await Store(writing).AddMemberAsync(
                team,
                GrantSubject.Of(account),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, await VersionAsync(account));
        Assert.Empty(await GroupsOfAsync(GrantSubject.Of(account)));
    }

    /// <summary>
    /// AUTHZ-GROUP-001: a group that already reaches a subject reports so, which is what
    /// a cycle looks like before it is written.
    /// </summary>
    [Fact]
    public async Task ReachesAsync_AGroupAlreadyHoldingTheSubject_ReportsSoAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        GroupId department = await GroupAsync(organization, "Department");
        GroupId team = await GroupAsync(organization, "Team");
        GroupId squad = await GroupAsync(organization, "Squad");

        await AddAsync(department, GrantSubject.Of(team));
        await AddAsync(team, GrantSubject.Of(squad));

        await using StoreContext reading = database.Context();

        Assert.True(await Store(reading).ReachesAsync(
            department,
            GrantSubject.Of(squad),
            TestContext.Current.CancellationToken));

        Assert.False(await Store(reading).ReachesAsync(
            squad,
            GrantSubject.Of(department),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-GROUP-001: an organization's groups are read together, by name, and no
    /// other organization's among them.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GROUP_001_AnOrganizationsGroupsAreReadByNameAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        OrganizationId elsewhere = await _deployment.OrganizationAsync(Noon);
        GroupId tellers = await GroupAsync(organization, "Tellers");
        GroupId auditors = await GroupAsync(organization, "Auditors");

        _ = await GroupAsync(elsewhere, "Operators");

        await using StoreContext reading = database.Context();

        IReadOnlyList<Group> groups = await Store(reading)
            .InAsync(organization, TestContext.Current.CancellationToken);

        Assert.Equal([auditors, tellers], groups.Select(group => group.Id));
        Assert.Equal(["Auditors", "Tellers"], groups.Select(group => group.Name));
    }

    /// <summary>
    /// AUTHZ-GROUP-001: a group nothing names is removed with its row, and the
    /// organization's other groups stay.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GROUP_001_ARemovedGroupIsGoneAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        GroupId removed = await GroupAsync(organization, "Retired");
        GroupId kept = await GroupAsync(organization, "Kept");

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await Store(writing).RemoveAsync(removed, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(removed, TestContext.Current.CancellationToken));
        Assert.Equal(
            [kept],
            (await Store(reading).InAsync(organization, TestContext.Current.CancellationToken))
                .Select(group => group.Id));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static GroupStore Store(StoreContext context) =>
        new(context, new DataConnections(context));

    private async Task<GroupId> GroupAsync(OrganizationId organization, string name)
    {
        var id = GroupId.New(TimeProvider.System);

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await Store(writing).CreateAsync(
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

        await Store(writing).AddMemberAsync(group, member, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task RemoveAsync(GroupId group, GrantSubject member)
    {
        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await Store(writing).RemoveMemberAsync(group, member, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<GroupId>> GroupsOfAsync(GrantSubject subject)
    {
        await using StoreContext reading = database.Context();

        IReadOnlyList<GroupId> groups = await Store(reading)
            .GroupsOfAsync(subject, TestContext.Current.CancellationToken);

        return [.. groups.OrderBy(group => group.Value)];
    }

    private async Task<long> VersionAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new GrantStore(reading, new DataConnections(reading))
            .VersionAsync(subject, TestContext.Current.CancellationToken);
    }
}
