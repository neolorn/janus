using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authorization.Groups;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Storage.Authorization.Groups;
using Janus.Storage.Identity.Audit;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What a change to a group leaves in the trail (AUTHZ-GROUP-001, OPS-CFG-007,
/// IDN-AUD-001).
/// </summary>
/// <remarks>
/// The port implementation is tested against the real database (D-156). One database
/// serves the class, so each test writes with an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class GroupAuditTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// AUTHZ-GROUP-001 and IDN-AUD-001 AC1: a member added is recorded with the group,
    /// its name, the member, the reason, the actor as both identities, and the
    /// organization the group belongs to.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AMemberAddedIsRecordedWithTheGroupAndTheMemberAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        var tellers = Group.Create(GroupId.New(TimeProvider.System), organization, "Tellers");
        var member = GrantSubject.Of(new SubjectId(Guid.NewGuid()));

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).MemberAddedAsync(
                tellers,
                member,
                "Counter staff.",
                actor,
                now,
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.GroupMemberAdded, read.Action);
        Assert.Equal(actor, read.ActingSubject);
        Assert.Equal(actor, read.EffectiveSubject);
        Assert.Equal(organization, read.Organization);
        Assert.Equal(tellers.Id.ToString(), read.Details["group"].GetString());
        Assert.Equal("Tellers", read.Details["name"].GetString());
        Assert.Equal("user", read.Details["memberType"].GetString());
        Assert.Equal(member.Value.ToString(), read.Details["memberId"].GetString());
        Assert.Equal("Counter staff.", read.Details["reason"].GetString());
    }

    /// <summary>
    /// AUTHZ-GROUP-001: a group created is recorded with its name and no member.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AGroupCreatedIsRecordedWithoutAMemberAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        var auditors = Group.Create(GroupId.New(TimeProvider.System), organization, "Auditors");

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).CreatedAsync(
                auditors,
                "Quarterly review.",
                actor,
                now,
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.GroupCreated, read.Action);
        Assert.Equal("Auditors", read.Details["name"].GetString());
        Assert.False(read.Details.ContainsKey("memberId"));
    }

    private GroupAudit Audit(StoreContext context) =>
        new(new AuditStore(context, _deployment.Keys, _deployment.Randomness), TimeProvider.System);

    private async Task<IReadOnlyList<AuditRecord>> RecordsAsync(SubjectId actor)
    {
        await using StoreContext reading = database.Context();

        return await new AuditStore(reading, _deployment.Keys, _deployment.Randomness)
            .FindBySubjectAsync(actor, TestContext.Current.CancellationToken);
    }
}
