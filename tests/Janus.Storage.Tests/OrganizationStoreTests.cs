using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// Organizations and memberships as their rows carry them (IDN-ORG-001, IDN-ORG-003,
/// IDN-MEM-001, IDN-MEM-002, OPS-DB-001).
/// </summary>
/// <remarks>
/// The port implementations are tested against the aggregates they translate, with the
/// real database (D-156).
/// </remarks>
[Trait("kind", "integration")]
public sealed class OrganizationStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromDays(30);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// An organization reads back as it was written, and the window it has not entered
    /// is absent rather than assumed.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ANewOrganization_ReadsBackAsItWasWrittenAsync()
    {
        OrganizationId id = await CreateAsync(Fresh("Acme Trading"));

        await using JanusDbContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.StartsWith("Acme Trading", read.Name, StringComparison.Ordinal);
        Assert.Equal(Noon, read.CreatedAt);
        Assert.False(read.IsSuspended);
        Assert.Null(read.ErasedAt);
    }

    /// <summary>
    /// An organization nobody created is not there.
    /// </summary>
    [Fact]
    public async Task FindAsync_AnOrganizationWithNoRow_ReadsAsNothingAsync()
    {
        await using JanusDbContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(
            new OrganizationId(Guid.NewGuid()),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ORG-003 AC5, IDN-PRIN-003: the erasure at the end of the window leaves the
    /// row where it is, and the identifier goes on resolving to it.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC5_TheRowSurvivesTheErasureAsync()
    {
        OrganizationId id = await CreateAsync(Fresh("Acme Trading"));

        await using (JanusDbContext deleting = database.Context())
        {
            OrganizationStore store = Store(deleting);
            Organization organization = Assert.IsType<Organization>(
                await store.FindAsync(id, TestContext.Current.CancellationToken));
            organization.RequestDeletion(Noon);
            organization.RecordErasure(Noon + Window, Window);

            await store.RecordAsync(organization, TestContext.Current.CancellationToken);
            await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(id, read.Id);
        Assert.Equal(Noon + Window, read.ErasedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC2: a cancellation clears the window on the row, so a later read
    /// finds an organization that is not suspended.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC2_ACancellationClearsTheWindowOnTheRowAsync()
    {
        OrganizationId id = await CreateAsync(Fresh("Acme Trading"));

        await using (JanusDbContext requesting = database.Context())
        {
            OrganizationStore store = Store(requesting);
            Organization organization = Assert.IsType<Organization>(
                await store.FindAsync(id, TestContext.Current.CancellationToken));
            organization.RequestDeletion(Noon);

            await store.RecordAsync(organization, TestContext.Current.CancellationToken);
            await requesting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (JanusDbContext cancelling = database.Context())
        {
            OrganizationStore store = Store(cancelling);
            Organization organization = Assert.IsType<Organization>(
                await store.FindAsync(id, TestContext.Current.CancellationToken));
            Assert.True(organization.IsSuspended);
            organization.CancelDeletion();

            await store.RecordAsync(organization, TestContext.Current.CancellationToken);
            await cancelling.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await Store(reading).FindAsync(id, TestContext.Current.CancellationToken));

        Assert.False(read.IsSuspended);
    }

    /// <summary>
    /// OPS-DB-001 AC2: the name is plaintext under the case-insensitive collation, so
    /// the database finds it whatever capitals it was typed with.
    /// </summary>
    [Fact]
    public async Task OPS_DB_001_AC2_TheOrganizationNameComparesWithoutRegardToCaseAsync()
    {
        string name = Fresh("Acme Trading");
        OrganizationId id = await CreateAsync(name);
        string shouted = name.ToUpperInvariant();

        await using JanusDbContext reading = database.Context();
        OrganizationRecord found = await reading.Organizations
            .SingleAsync(
                organization => organization.Name == shouted,
                TestContext.Current.CancellationToken);

        Assert.NotEqual(shouted, name);
        Assert.Equal(id, found.Id);
    }

    /// <summary>
    /// IDN-MEM-002 AC1: the schema takes more than one membership for an account. What
    /// forbids a second one is a setting, not the table.
    /// </summary>
    [Fact]
    public async Task IDN_MEM_002_AC1_TheSchemaTakesMoreThanOneMembershipPerAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId first = await CreateAsync(Fresh("Acme Trading"));
        OrganizationId second = await CreateAsync(Fresh("Beta Works"));

        await using (JanusDbContext writing = database.Context())
        {
            MembershipStore store = Memberships(writing);
            await store.CreateAsync(
                Membership.Create(NewId(), subject, first, Noon),
                TestContext.Current.CancellationToken);
            await store.CreateAsync(
                Membership.Create(NewId(), subject, second, Noon),
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        IReadOnlyList<Membership> held = await Memberships(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, held.Count);
    }

    /// <summary>
    /// IDN-MEM-001 AC1, IDN-PRIN-003: ending a membership writes an instant onto the
    /// row and leaves the account and the organization where they were.
    /// </summary>
    [Fact]
    public async Task IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesAndTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await CreateAsync(Fresh("Acme Trading"));
        MembershipId id = NewId();

        await using (JanusDbContext writing = database.Context())
        {
            await Memberships(writing).CreateAsync(
                Membership.Create(id, subject, organization, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DateTimeOffset ended = Noon.AddDays(400);

        await using (JanusDbContext ending = database.Context())
        {
            MembershipStore store = Memberships(ending);
            IReadOnlyList<Membership> held = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);
            held[0].End(ended);

            await store.RecordAsync(held[0], TestContext.Current.CancellationToken);
            await ending.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        IReadOnlyList<Membership> read = await Memberships(reading).FindByOrganizationAsync(
            organization,
            TestContext.Current.CancellationToken);

        Assert.Single(read);
        Assert.Equal(ended, read[0].EndedAt);
        Assert.NotNull(await Store(reading).FindAsync(
            organization,
            TestContext.Current.CancellationToken));
        Assert.True(await reading.Accounts
            .AnyAsync(account => account.Subject == subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A membership recorded against no row is a fault in the caller.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AMembershipWithNoRow_ThrowsAsync()
    {
        await using JanusDbContext context = database.Context();

        var membership = Membership.Create(
            NewId(),
            new SubjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Noon);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Memberships(context).RecordAsync(membership, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static MembershipId NewId() => new(Guid.CreateVersion7());

    // The tests of a class share one database, so each names its own organization.
    private static string Fresh(string name) => name + " " + Guid.NewGuid().ToString("N");

    private static OrganizationStore Store(JanusDbContext context) => new(context);

    private static MembershipStore Memberships(JanusDbContext context) => new(context);

    private async ValueTask<OrganizationId> CreateAsync(string name)
    {
        var id = new OrganizationId(Guid.CreateVersion7());

        await using JanusDbContext writing = database.Context();
        await Store(writing).CreateAsync(
            Organization.Create(id, name, Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
