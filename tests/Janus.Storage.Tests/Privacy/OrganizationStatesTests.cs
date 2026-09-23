using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Privacy.Erasures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The erasure at the end of an organization deletion window, over the rows it reaches
/// (IDN-ORG-003, IDN-ORG-005, IDN-PRIN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class OrganizationStatesTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromDays(30);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// IDN-ORG-003: a window that has run out is what the pass reads, and one that
    /// began later and one nobody opened are not.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_TheWindowsThatHaveRunOutAreWhatThePassReadsAsync()
    {
        OrganizationId running = await DeletingAsync(Noon);
        OrganizationId standing = await CreateAsync();
        OrganizationId recent = await DeletingAsync(Noon + Window + TimeSpan.FromDays(1));

        await using StoreContext reading = database.Context();

        IReadOnlyList<OrganizationId> reached =
        [
            .. (await States(reading).DeletingSinceAsync(
                    Noon + Window,
                    TestContext.Current.CancellationToken))
                .Select(deletion => deletion.Organization),
        ];

        Assert.Contains(running, reached);
        Assert.DoesNotContain(standing, reached);
        Assert.DoesNotContain(recent, reached);
    }

    /// <summary>
    /// IDN-ORG-003 AC5, IDN-PRIN-003: the erasure leaves the row where it is and the
    /// identifier resolving, with what the organization was called replaced by that
    /// identifier and the instant it executed written down.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC5_TheRowSurvivesAndTheNameBecomesTheIdentifierAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);

        _ = await EraseAsync(organization, Noon + Window);

        await using StoreContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await new OrganizationStore(reading).FindAsync(
                organization,
                TestContext.Current.CancellationToken));

        Assert.Equal(Noon + Window, read.ErasedAt);
        Assert.Equal(organization.Value.ToString("D", CultureInfo.InvariantCulture), read.Name);
    }

    /// <summary>
    /// IDN-ORG-003: the erasure ends every current membership of the organization and
    /// answers with how many it ended, leaving an already ended one where it stands.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_TheErasureEndsEveryCurrentMembershipAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);
        SubjectId first = await _deployment.AccountAsync(Noon);
        SubjectId second = await _deployment.AccountAsync(Noon);
        SubjectId gone = await _deployment.AccountAsync(Noon);

        await PlaceAsync(first, organization, until: null);
        await PlaceAsync(second, organization, until: null);
        await PlaceAsync(gone, organization, Noon.AddDays(1));

        Assert.Equal(2, await EraseAsync(organization, Noon + Window));

        await using StoreContext reading = database.Context();
        IReadOnlyList<Membership> held = await new MembershipStore(reading)
            .FindByOrganizationAsync(organization, TestContext.Current.CancellationToken);

        Assert.All(held, membership => Assert.False(membership.IsCurrent));
        Assert.Equal(
            Noon.AddDays(1),
            Assert.Single(held, membership => membership.Subject == gone).EndedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC3: the erasure does not execute before the window elapses, so a
    /// caller that asks for one a day early writes nothing.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC3_AnErasureBeforeTheWindowElapsesWritesNothingAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await EraseAsync(organization, Noon + Window - TimeSpan.FromDays(1)));

        await using StoreContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await new OrganizationStore(reading).FindAsync(
                organization,
                TestContext.Current.CancellationToken));

        Assert.Null(read.ErasedAt);
    }

    private static OrganizationStates States(StoreContext context) =>
        new(context, new OrganizationStore(context), new MembershipStore(context));

    private async ValueTask<int> EraseAsync(OrganizationId organization, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        await work.BeginAsync(TestContext.Current.CancellationToken);

        int ended = await States(writing).EraseAsync(
            organization,
            at,
            Window,
            TestContext.Current.CancellationToken);

        await work.CommitAsync(TestContext.Current.CancellationToken);

        return ended;
    }

    private async ValueTask<OrganizationId> CreateAsync()
    {
        var id = new OrganizationId(Guid.CreateVersion7());

        await using StoreContext writing = database.Context();
        await new OrganizationStore(writing).CreateAsync(
            Organization.Create(id, "Acme " + id.Value.ToString("N"), Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async ValueTask<OrganizationId> DeletingAsync(DateTimeOffset at)
    {
        OrganizationId id = await CreateAsync();

        await using StoreContext writing = database.Context();
        OrganizationStore store = new(writing);
        Organization organization = Assert.IsType<Organization>(
            await store.FindAsync(id, TestContext.Current.CancellationToken));

        _ = organization.RequestDeletion(at);

        await store.RecordAsync(organization, TestContext.Current.CancellationToken);
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

        if (until is DateTimeOffset ended)
        {
            membership.End(ended);
        }

        await using StoreContext writing = database.Context();
        await new MembershipStore(writing).CreateAsync(
            membership,
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
