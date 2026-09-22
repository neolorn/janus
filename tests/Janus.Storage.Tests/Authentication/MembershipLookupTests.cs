using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Policies;
using Janus.Storage.Identity.Organizations;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a policy is resolved from: the organizations a principal belongs to now, and
/// no other fact about them (AUTH-PRIN-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class MembershipLookupTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-PRIN-002 AC1: what a principal is judged by is read from the memberships
    /// they hold and from nothing else, and a membership that has ended is not one
    /// they hold.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_PRIN_002_AC1_OnlyALiveMembershipResolvesAPolicyAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId current = await OrganizationAsync("Acme Trading");
        OrganizationId past = await OrganizationAsync("Beta Works");

        await PlaceAsync(subject, current, until: null);
        await PlaceAsync(subject, past, Noon.AddDays(400));

        await using JanusDbContext reading = database.Context();

        Assert.Equal(
            [current],
            await new MembershipLookup(reading).OfAsync(
                subject,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-PRIN-002 AC3: a principal in no organization resolves to nothing, which
    /// is what leaves them under the system policy.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_PRIN_002_AC3_APrincipalInNoOrganizationResolvesToNothingAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using JanusDbContext reading = database.Context();

        Assert.Empty(await new MembershipLookup(reading).OfAsync(
            subject,
            TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // The tests of a class share one database, so each names its own organization.
    private static string Fresh(string name) => name + " " + Guid.NewGuid().ToString("N");

    private async ValueTask<OrganizationId> OrganizationAsync(string name)
    {
        var id = new OrganizationId(Guid.CreateVersion7());

        await using JanusDbContext writing = database.Context();
        await new OrganizationStore(writing).CreateAsync(
            Organization.Create(id, Fresh(name), Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async ValueTask PlaceAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset? until)
    {
        Membership membership = Membership
            .Create(
                new MembershipId(Guid.CreateVersion7()),
                subject,
                organization,
                [],
                multiple: true,
                Noon)
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        if (until is { } ended)
        {
            membership.End(ended);
        }

        await using JanusDbContext writing = database.Context();
        await new MembershipStore(writing).CreateAsync(
            membership,
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
