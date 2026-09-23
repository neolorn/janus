using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authorization.Resources;
using Janus.Core;
using Janus.Storage.Authorization.Resources;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// The ancestry closure as a create, a move and a bulk import leave it
/// (AUTHZ-INHERIT-002, AUTHZ-INHERIT-003, CONV-TEST-003).
/// </summary>
/// <remarks>
/// The highest-risk area in the design: a wrong row here means someone sees a record
/// they should not, silently. The fixture is a real structure, several levels deep
/// (CONV-TEST-005).
/// </remarks>
[Trait("kind", "integration")]
public sealed class AncestryStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: creating a record writes its ancestry beside it, in the
    /// same transaction.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_CreatingARecordWritesItsAncestryAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");
        ResourceReference folder = Reference("folder");
        ResourceReference document = Reference("document");

        await RegisterAsync(workspace, organization, containedIn: null);
        await RegisterAsync(folder, organization, workspace);
        await RegisterAsync(document, organization, folder);

        Assert.Equal(
            [document, folder, workspace],
            await AncestryAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: a transaction that rolls back leaves neither the record
    /// nor its ancestry.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_ARollbackLeavesNeitherTheRecordNorItsAncestryAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await Store(writing).RegisterAsync(
                RegisteredResource.Create(workspace, organization, subject: null, containedIn: null),
                TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(workspace, TestContext.Current.CancellationToken));
        Assert.Empty(await Store(reading).AncestryAsync(workspace, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC2, AUTHZ-INHERIT-003 AC1: moving a record beneath what was
    /// its own sibling carries every record beneath it with the move.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_003_AC1_MovingASubtreeBeneathItsFormerSiblingAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");
        ResourceReference one = Reference("folder");
        ResourceReference other = Reference("folder");
        ResourceReference document = Reference("document");

        await RegisterAsync(workspace, organization, containedIn: null);
        await RegisterAsync(one, organization, workspace);
        await RegisterAsync(other, organization, workspace);
        await RegisterAsync(document, organization, one);

        await MoveAsync(one, other);

        Assert.Equal([one, other, workspace], await AncestryAsync(one));
        Assert.Equal([document, one, other, workspace], await AncestryAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC2: a record moved out of every container keeps nothing above
    /// it, and neither does anything beneath it.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC2_MovingOutOfEveryContainerClearsWhatWasAboveAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");
        ResourceReference folder = Reference("folder");
        ResourceReference document = Reference("document");

        await RegisterAsync(workspace, organization, containedIn: null);
        await RegisterAsync(folder, organization, workspace);
        await RegisterAsync(document, organization, folder);

        await MoveAsync(folder, containedIn: null);

        Assert.Equal([folder], await AncestryAsync(folder));
        Assert.Equal([document, folder], await AncestryAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-003 AC2: two moves of overlapping subtrees, one after the other in
    /// the order the database serialized them, leave one ancestry and not a mixture.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_003_AC2_ConcurrentMovesOfOverlappingSubtreesAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference left = Reference("workspace");
        ResourceReference right = Reference("workspace");
        ResourceReference folder = Reference("folder");
        ResourceReference document = Reference("document");

        await RegisterAsync(left, organization, containedIn: null);
        await RegisterAsync(right, organization, containedIn: null);
        await RegisterAsync(folder, organization, left);
        await RegisterAsync(document, organization, folder);

        await Task.WhenAll(MoveAsync(folder, right), MoveAsync(document, right));

        IReadOnlyList<ResourceReference> ancestry = await AncestryAsync(document);

        Assert.Contains(document, ancestry);
        Assert.Contains(right, ancestry);
        Assert.DoesNotContain(left, ancestry);
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC3: a bulk import produces the ancestry every record of it
    /// should have.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC3_ABulkImportProducesCorrectAncestryAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");
        List<RegisteredResource> batch =
            [RegisteredResource.Create(workspace, organization, subject: null, containedIn: null)];
        List<ResourceReference> folders = [];

        for (int index = 0; index < 99; index++)
        {
            ResourceReference folder = Reference("folder");
            folders.Add(folder);
            batch.Add(RegisteredResource.Create(folder, organization, subject: null, workspace));

            for (int beneath = 0; beneath < 100; beneath++)
            {
                batch.Add(RegisteredResource.Create(Reference("document"), organization, subject: null, folder));
            }
        }

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await Store(writing).RegisterManyAsync(batch, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(10_000, await reading.Resources
            .CountAsync(row => row.Organization == organization, TestContext.Current.CancellationToken));

        Assert.Equal(
            (99 * 100 * 3) + (99 * 2) + 1,
            await reading.Ancestry
                .CountAsync(row => row.Organization == organization, TestContext.Current.CancellationToken));

        Assert.Equal([folders[7], workspace], await AncestryAsync(folders[7]));
    }

    /// <summary>
    /// AUTHZ-INHERIT-003 AC3: no row of the closure names a record that is not
    /// registered, whatever was created and moved.
    /// </summary>
    [Fact]
    public async Task AUTHZ_INHERIT_003_AC3_NoAncestryRowIsOrphanedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        ResourceReference workspace = Reference("workspace");
        ResourceReference folder = Reference("folder");
        ResourceReference document = Reference("document");

        await RegisterAsync(workspace, organization, containedIn: null);
        await RegisterAsync(folder, organization, workspace);
        await RegisterAsync(document, organization, folder);
        await MoveAsync(document, workspace);
        await MoveAsync(folder, containedIn: null);

        await using StoreContext reading = database.Context();

        List<AncestryRecord> rows = await reading.Ancestry
            .Where(row => row.Organization == organization)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.All(rows, row => Assert.True(
            reading.Resources.Any(resource => resource.Type == row.Type && resource.Id == row.Id)
            && reading.Resources.Any(resource =>
                resource.Type == row.AncestorType && resource.Id == row.AncestorId)));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static ResourceStore Store(StoreContext context) =>
        new(context, new DataConnections(context));

    private static ResourceReference Reference(string type) =>
        new(ResourceType.Parse(type), ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task RegisterAsync(
        ResourceReference reference,
        OrganizationId organization,
        ResourceReference? containedIn)
    {
        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await Store(writing).RegisterAsync(
            RegisteredResource.Create(reference, organization, subject: null, containedIn),
            TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task MoveAsync(ResourceReference reference, ResourceReference? containedIn)
    {
        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await Store(writing).MoveAsync(reference, containedIn, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<ResourceReference>> AncestryAsync(ResourceReference reference)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).AncestryAsync(reference, TestContext.Current.CancellationToken);
    }
}
