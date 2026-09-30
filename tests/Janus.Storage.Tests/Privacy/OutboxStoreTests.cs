using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;
using Janus.Storage.Privacy.Outbox;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The takedown's and the erasure's deliveries on the outbox, and what the operator's
/// screens read of them (IDN-LIFE-003, IDN-LIFE-003a, IDN-LIFE-003b).
/// </summary>
[Trait("kind", "integration")]
public sealed class OutboxStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// IDN-LIFE-003 AC2: the latest takedown of an account is read with each
    /// confirmation and when it was given, and a delivery of another kind or an
    /// earlier takedown is not what is read.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AC2_TheLatestTakedownIsReadWithItsConfirmationsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var earlier = Delivery.Of(subject, SubjectEventKind.TakedownExecuted, Noon);
        var latest = Delivery.Of(subject, SubjectEventKind.TakedownExecuted, Noon + TimeSpan.FromDays(10));
        var restriction = Delivery.Of(subject, SubjectEventKind.RestrictionChanged, Noon + TimeSpan.FromDays(11), restricted: true);

        await using (StoreContext writing = database.Context())
        {
            OutboxStore outbox = Store(writing, Noon);

            await outbox.AddAsync(earlier, TestContext.Current.CancellationToken);
            await outbox.AddAsync(latest, TestContext.Current.CancellationToken);
            await outbox.AddAsync(restriction, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DateTimeOffset confirmedAt = Noon + TimeSpan.FromDays(10) + TimeSpan.FromMinutes(5);

        await using (StoreContext confirming = database.Context())
        {
            latest.Confirm("records");

            await Store(confirming, confirmedAt).RecordAsync(latest, TestContext.Current.CancellationToken);
            await confirming.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        DeliveryProgress progress = Assert.IsType<DeliveryProgress>(
            await Store(reading, Noon).LatestAsync(
                subject,
                SubjectEventKind.TakedownExecuted,
                TestContext.Current.CancellationToken));

        Assert.Equal(latest.Id, progress.Delivery.Id);
        Assert.Equal(SubjectEventKind.TakedownExecuted, progress.Delivery.Kind);
        Assert.Equal(confirmedAt, Assert.Contains("records", progress.ConfirmedAt));
        Assert.Single(progress.ConfirmedAt);
    }

    /// <summary>
    /// IDN-LIFE-003: an account never taken down has no takedown to read.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AnAccountNeverTakenDownHasNoTakedownToReadAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading, Noon).LatestAsync(
            subject,
            SubjectEventKind.TakedownExecuted,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-003b AC2, chapter 09 section 8a: every erasure whose delivery is
    /// outstanding, awaiting subscribers or failed, is read in one query with its
    /// confirmations, and a completed erasure or a delivery of another kind is not.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC2_EveryOutstandingErasureIsReadInOneQueryAsync()
    {
        SubjectId awaiting = await _deployment.AccountAsync(Noon);
        SubjectId failed = await _deployment.AccountAsync(Noon);
        SubjectId complete = await _deployment.AccountAsync(Noon);
        var waiting = Delivery.Of(awaiting, SubjectEventKind.ErasureRequested, Noon);
        var spent = Delivery.Of(failed, SubjectEventKind.ErasureRequested, Noon, reason: ErasureReason.MinorTakedown);
        var done = Delivery.Of(complete, SubjectEventKind.ErasureRequested, Noon);
        var takedown = Delivery.Of(awaiting, SubjectEventKind.TakedownExecuted, Noon);

        spent.Fail();
        done.Complete();

        await using (StoreContext writing = database.Context())
        {
            OutboxStore outbox = Store(writing, Noon);

            foreach (Delivery delivery in new[] { waiting, spent, done, takedown })
            {
                await outbox.AddAsync(delivery, TestContext.Current.CancellationToken);
            }

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DateTimeOffset confirmedAt = Noon + TimeSpan.FromMinutes(5);

        await using (StoreContext confirming = database.Context())
        {
            waiting.Confirm("newsletter");

            await Store(confirming, confirmedAt).RecordAsync(waiting, TestContext.Current.CancellationToken);
            await confirming.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        IReadOnlyList<DeliveryProgress> outstanding = await Store(reading, Noon).OutstandingAsync(
            SubjectEventKind.ErasureRequested,
            TestContext.Current.CancellationToken);

        DeliveryProgress first = Assert.Single(outstanding, progress => progress.Delivery.Id == waiting.Id);
        DeliveryProgress second = Assert.Single(outstanding, progress => progress.Delivery.Id == spent.Id);

        Assert.Equal(confirmedAt, Assert.Contains("newsletter", first.ConfirmedAt));
        Assert.Equal(ErasureStatus.Failed, second.Delivery.Status);
        Assert.Equal(ErasureReason.MinorTakedown, second.Delivery.Reason);
        Assert.DoesNotContain(outstanding, progress => progress.Delivery.Id == done.Id);
        Assert.DoesNotContain(outstanding, progress => progress.Delivery.Kind is not SubjectEventKind.ErasureRequested);
    }

    /// <summary>
    /// IDN-LIFE-003b: one delivery is read by its identifier with its confirmations,
    /// and an identifier naming no delivery reads nothing.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_OneDeliveryIsReadWithItsConfirmationsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var delivery = Delivery.Of(subject, SubjectEventKind.ErasureRequested, Noon);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing, Noon).AddAsync(delivery, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext confirming = database.Context())
        {
            delivery.Confirm("records");

            await Store(confirming, Noon + TimeSpan.FromMinutes(1))
                .RecordAsync(delivery, TestContext.Current.CancellationToken);
            await confirming.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        OutboxStore store = Store(reading, Noon);
        DeliveryProgress progress = Assert.IsType<DeliveryProgress>(
            await store.ProgressAsync(delivery.Id, TestContext.Current.CancellationToken));

        Assert.Equal(subject, progress.Delivery.Subject);
        Assert.Equal(Noon + TimeSpan.FromMinutes(1), Assert.Contains("records", progress.ConfirmedAt));
        Assert.Null(await store.ProgressAsync(
            DeliveryId.Of(Noon),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// DR-016 AC5: the erasures complete without a confirmation from the off-host
    /// ledger are read oldest first, a page at a time; one the ledger confirmed, one
    /// still outstanding and a delivery of another kind are not.
    /// </summary>
    [Fact]
    public async Task DR_016_AC5_TheCompletedErasuresWithoutALineAreReadAPageAtATimeAsync()
    {
        DateTimeOffset early = new(2025, 1, 6, 9, 0, 0, TimeSpan.Zero);
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var oldest = Delivery.Of(subject, SubjectEventKind.ErasureRequested, early);
        var older = Delivery.Of(subject, SubjectEventKind.ErasureRequested, early.AddMinutes(1));
        var written = Delivery.Of(subject, SubjectEventKind.ErasureRequested, early.AddMinutes(2));
        var awaiting = Delivery.Of(subject, SubjectEventKind.ErasureRequested, early.AddMinutes(3));
        var takedown = Delivery.Of(subject, SubjectEventKind.TakedownExecuted, early.AddMinutes(4));

        oldest.Complete();
        older.Complete();
        written.Confirm("erasure-ledger");
        written.Complete();
        takedown.Complete();

        await using (StoreContext writing = database.Context())
        {
            OutboxStore outbox = Store(writing, Noon);

            foreach (Delivery delivery in new[] { oldest, older, written, awaiting, takedown })
            {
                await outbox.AddAsync(delivery, TestContext.Current.CancellationToken);
            }

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext confirming = database.Context())
        {
            await Store(confirming, Noon).RecordAsync(written, TestContext.Current.CancellationToken);
            await confirming.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        OutboxStore store = Store(reading, Noon);
        DeliveryId[] ours = [oldest.Id, older.Id, written.Id, awaiting.Id, takedown.Id];

        IReadOnlyList<Delivery> every = await store.UnledgeredAsync(0, 1000, TestContext.Current.CancellationToken);
        IReadOnlyList<Delivery> first = await store.UnledgeredAsync(0, 1, TestContext.Current.CancellationToken);
        IReadOnlyList<Delivery> second = await store.UnledgeredAsync(1, 1, TestContext.Current.CancellationToken);

        Assert.Equal([oldest.Id, older.Id], every.Where(held => ours.Contains(held.Id)).Select(held => held.Id));
        Assert.Equal([oldest.Id], first.Select(held => held.Id));
        Assert.Equal([older.Id], second.Select(held => held.Id));
    }

    private static OutboxStore Store(StoreContext context, DateTimeOffset now) =>
        new(context, new FixedTime(now));
}
