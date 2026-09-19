using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Authorization.Groups;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// What is held for the length of one operation, and what is not
/// (AUTHZ-GROUP-002, AUTHZ-CACHE-001, AUTHZ-PRIN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SubjectSetsTests
{
    // What a held entry may not be keyed or shaped by: an outcome for one action on
    // one record is exactly what AUTHZ-CACHE-001 refuses to hold.
    private static readonly string[] NotHeld = ["Permission", "Resource", "Outcome"];

    /// <summary>
    /// AUTHZ-GROUP-002 AC1: ten checks in one operation read the groups once, the set
    /// being resolved for the operation rather than for the check.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_002_AC1_TenChecksInOneOperationResolveMembershipOnceAsync()
    {
        var groups = new GroupsInMemory();
        var grants = new GrantsInMemory();
        var sets = new SubjectSets(groups, grants);
        var context = AccessContext.Of(Subject());

        for (int check = 0; check < 10; check++)
        {
            await sets.OfAsync(context, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, groups.Reads);
        Assert.Equal(1, grants.Reads);
    }

    /// <summary>
    /// AUTHZ-GROUP-002 AC2, AUTHZ-CACHE-001: what is held is the group set and the
    /// counter it was read at, so the entry is orphaned by the next change rather than
    /// waiting for an expiry.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC3_TheHeldSetCarriesTheCounterItWasReadAtAsync()
    {
        var groups = new GroupsInMemory();
        var grants = new GrantsInMemory();
        SubjectId subject = Subject();

        grants.Bump(subject);
        grants.Bump(subject);

        SubjectSet resolved = await new SubjectSets(groups, grants)
            .OfAsync(AccessContext.Of(subject), TestContext.Current.CancellationToken);

        Assert.Equal(2, resolved.Version);
        Assert.Equal([subject.Value], resolved.Accounts);
    }

    /// <summary>
    /// AUTHZ-GROUP-001 AC1, AC2: a subject in a group inside a group holds what the
    /// outer one holds, and nothing in the shape fixes how deep that may go.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GROUP_001_AC2_NestingIsFollowedToWhateverDepthIsWrittenAsync()
    {
        var groups = new GroupsInMemory();
        var grants = new GrantsInMemory();
        SubjectId subject = Subject();
        var organization = new OrganizationId(Guid.NewGuid());

        List<GroupId> chain = [];

        for (int depth = 0; depth < 12; depth++)
        {
            var group = GroupId.New(TimeProvider.System);
            chain.Add(group);

            await groups.CreateAsync(
                Group.Create(group, organization, "Group " + depth),
                TestContext.Current.CancellationToken);

            await groups.AddMemberAsync(
                group,
                depth == 0 ? GrantSubject.Of(subject) : GrantSubject.Of(chain[depth - 1]),
                TestContext.Current.CancellationToken);
        }

        SubjectSet resolved = await new SubjectSets(groups, grants)
            .OfAsync(AccessContext.Of(subject), TestContext.Current.CancellationToken);

        Assert.Equal(
            chain.Select(group => group.Value).Order(),
            resolved.Groups.Order());
    }

    /// <summary>
    /// AUTHZ-PRIN-003 AC2: a principal the library holds no account for resolves to
    /// nothing, and nothing confers nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_PRIN_003_AC2_APrincipalWithNoAccountResolvesToNothingAsync()
    {
        SubjectSet resolved = await new SubjectSets(new GroupsInMemory(), new GrantsInMemory())
            .OfAsync(
                AccessContext.Of(SystemPrincipal.ForOrganization(
                    "retention",
                    "the nightly sweep",
                    new OrganizationId(Guid.NewGuid()))),
                TestContext.Current.CancellationToken);

        Assert.Empty(resolved.Accounts);
        Assert.Empty(resolved.Groups);
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC4: what is held is keyed by the account alone, so no entry can
    /// be keyed on an action or a record and go stale when one of them changes.
    /// </summary>
    [Fact]
    public void AUTHZ_CACHE_001_AC4_NothingIsKeyedOnASubjectActionAndRecord()
    {
        FieldInfo held = Assert.Single(
            typeof(SubjectSets).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType.IsGenericType);

        Assert.Equal([typeof(SubjectId), typeof(SubjectSet)], held.FieldType.GetGenericArguments());

        Assert.All(
            typeof(SubjectSet).GetProperties(),
            property => Assert.DoesNotContain(property.Name, NotHeld, StringComparer.Ordinal));
    }

    private static SubjectId Subject()
    {
        using var randomness = RandomNumberGenerator.Create();

        return SubjectId.New(randomness);
    }
}
