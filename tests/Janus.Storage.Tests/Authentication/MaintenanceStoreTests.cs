using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authentication.Maintenance;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the licences and permits and the maintenance log keep (OPS-MAINT-001).
/// </summary>
/// <remarks>
/// One database serves the class. The licences are replaced whole, so each test that
/// reads them writes the list it reads; the log is only appended to, so each test
/// reads its own entries by their identifiers.
/// </remarks>
[Trait("kind", "integration")]
public sealed class MaintenanceStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Actor = new(Guid.Parse("5c1e9f47-2a3b-4d6e-8f0a-1b2c3d4e5f60"));

    private static readonly Licence Operating = new(
        new LicenceId(Guid.Parse("7d1c5b2e-3f4a-4b6c-8d9e-0a1b2c3d4e5f")),
        LicenceKind.Licence,
        "Operating licence",
        Noon.AddYears(3),
        RenewedAt: null);

    private static readonly Licence Premises = new(
        new LicenceId(Guid.Parse("1e2d3c4b-5a6f-4e7d-9c8b-7a6f5e4d3c2b")),
        LicenceKind.Permit,
        "Premises permit",
        Noon.AddMonths(6),
        Noon.AddMonths(-6));

    /// <summary>
    /// OPS-MAINT-001 AC1: the expiry dates are stored and read back soonest to lapse
    /// first, and a replacement leaves exactly the list it was given.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC1_TheExpiryDatesReadBackAsTheyWereReplacedAsync()
    {
        await ReplacedAsync([Operating, Premises]);

        Assert.Equal([Premises, Operating], await LicencesAsync());

        Licence renewed = Operating with { ExpiresAt = Noon.AddYears(6), RenewedAt = Noon };

        await ReplacedAsync([renewed]);

        Assert.Equal([renewed], await LicencesAsync());
    }

    /// <summary>
    /// OPS-MAINT-001 AC3: an entry reads back dated and with the subject who performed
    /// the task, and the log reads the most recently performed first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC3_AnEntryReadsBackDatedAndWithItsActorAsync()
    {
        var earlier = new MaintenanceEntry(
            MaintenanceEntryId.Of(Noon),
            MaintenanceTask.ApproverReview,
            Noon.AddDays(-7),
            Actor,
            Note: null);
        var later = new MaintenanceEntry(
            MaintenanceEntryId.Of(Noon),
            MaintenanceTask.EnvelopeRotation,
            Noon.AddDays(-1),
            Actor,
            "Envelope resealed; escrow recovery tested.");

        await using (StoreContext writing = database.Context())
        {
            var store = new MaintenanceStore(writing);

            await store.RecordAsync(earlier, TestContext.Current.CancellationToken);
            await store.RecordAsync(later, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        IReadOnlyList<MaintenanceEntry> log = await new MaintenanceStore(reading)
            .LogAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [later, earlier],
            log.Where(entry => entry.Id == earlier.Id || entry.Id == later.Id));
    }

    private async Task ReplacedAsync(IReadOnlyList<Licence> licences)
    {
        await using StoreContext writing = database.Context();

        await new MaintenanceStore(writing).ReplaceLicencesAsync(licences, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<Licence>> LicencesAsync()
    {
        await using StoreContext reading = database.Context();

        return await new MaintenanceStore(reading).LicencesAsync(TestContext.Current.CancellationToken);
    }
}
