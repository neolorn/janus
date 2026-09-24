using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Identity.Organizations;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The membership an administrator ends, over the rows (IDN-MEM-001, REG-MAIL-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class MembershipEndingTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-MEM-001 AC1 and AC2: the current membership of the organization ends and its
    /// row stays, carrying when it began and when it ended; a second end, or an end in
    /// an organization the account never belonged to, finds nothing; and the account
    /// may be invited into the organization again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC2_TheMembershipEndsAndItsRecordStaysAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        OrganizationId elsewhere = await _deployment.OrganizationAsync(Noon);
        MembershipId attached = await AttachAsync(subject, organization, Noon);

        Assert.Equal(attached, await EndAsync(subject, organization, Noon.AddDays(1)));
        Assert.Null(await EndAsync(subject, organization, Noon.AddDays(2)));
        Assert.Null(await EndAsync(subject, elsewhere, Noon.AddDays(2)));

        MembershipId again = await AttachAsync(subject, organization, Noon.AddDays(3));

        await using StoreContext reading = database.Context();

        var held = (await new MembershipStore(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken))
            .ToDictionary(membership => membership.Id);

        Assert.Equal(2, held.Count);
        Assert.Equal((Noon, Noon.AddDays(1)), (held[attached].CreatedAt, held[attached].EndedAt));
        Assert.True(held[again].IsCurrent);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private async Task<MembershipId> AttachAsync(SubjectId subject, OrganizationId organization, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();

        MembershipId attached = (await new MembershipAttachment(
                    new MembershipStore(writing),
                    new GrantStore(writing, new DataConnections(writing)),
                    TimeProvider.System)
                .AttachAsync(
                    subject,
                    organization,
                    [],
                    [],
                    subject,
                    "invitation:reason",
                    multiple: false,
                    at,
                    TestContext.Current.CancellationToken))
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return attached;
    }

    private async Task<MembershipId?> EndAsync(SubjectId subject, OrganizationId organization, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();

        MembershipId? ended = await new MembershipEnding(new MembershipStore(writing))
            .EndAsync(subject, organization, at, TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return ended;
    }
}
