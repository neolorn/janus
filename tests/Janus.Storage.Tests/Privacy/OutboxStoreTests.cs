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

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

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

        IReadOnlyList<DeliveryId> every = await store.UnledgeredAsync(0, 1000, TestContext.Current.CancellationToken);
        IReadOnlyList<DeliveryId> first = await store.UnledgeredAsync(0, 1, TestContext.Current.CancellationToken);
        IReadOnlyList<DeliveryId> second = await store.UnledgeredAsync(1, 1, TestContext.Current.CancellationToken);

        Assert.Equal([oldest.Id, older.Id], every.Where(held => ours.Contains(held)));
        Assert.Equal([oldest.Id], first);
        Assert.Equal([older.Id], second);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: an outbox row is claimed whole by one
    /// conditional update, so of several passes that reach it at once one takes it; while
    /// the claim stands no pass reads the row as due and none claims it; a renewal moves
    /// the end of the claim and is its holder's alone; and once the claim has timed out
    /// the next pass takes the row over, after which the first pass renews, confirms and
    /// records nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ADeliveryIsClaimedByOnePassAndWrittenOnlyUnderItsClaimAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var delivery = Delivery.Of(subject, SubjectEventKind.ErasureRequested, Noon);

        await AddedAsync(delivery);

        DeliveryClaim?[] claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimedAsync(delivery.Id, Noon)));

        DeliveryClaim claim = Assert.Single(claims, one => one is not null)!.Value;

        Assert.Equal(Noon + Timeout, claim.Until);
        Assert.Null(await ClaimedAsync(delivery.Id, Noon + Timeout - TimeSpan.FromSeconds(1)));

        await using (StoreContext reading = database.Context())
        {
            Assert.DoesNotContain(
                delivery.Id,
                await Store(reading, Noon).DueAsync(Noon + Timeout - TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        }

        DeliveryClaim renewed;

        await using (StoreContext renewing = database.Context())
        {
            OutboxStore store = Store(renewing, Noon);

            renewed = await store.RenewAsync(claim, Noon.AddMinutes(1), Timeout, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The claim was not renewed.");

            Assert.Equal(Noon.AddMinutes(1) + Timeout, renewed.Until);
            Assert.Null(await store.RenewAsync(claim, Noon.AddMinutes(1), Timeout, TestContext.Current.CancellationToken));
        }

        Assert.Null(await ClaimedAsync(delivery.Id, Noon + Timeout));
        Assert.False(await ConfirmedAsync(claim, "records", Noon.AddMinutes(1)));
        Assert.True(await ConfirmedAsync(renewed, "records", Noon.AddMinutes(1)));

        DeliveryClaim taken = await ClaimedAsync(delivery.Id, renewed.Until)
            ?? throw new Xunit.Sdk.XunitException("The timed-out claim was not taken over.");

        Assert.False(await ConfirmedAsync(renewed, "warehouse", renewed.Until));

        delivery.Attempted(renewed.Until, TimeSpan.FromSeconds(30), 2.0m, jitter: 1);
        delivery.Complete();

        await using (StoreContext recording = database.Context())
        {
            OutboxStore store = Store(recording, Noon);

            Assert.Null(await store.RenewAsync(renewed, renewed.Until, Timeout, TestContext.Current.CancellationToken));
            Assert.False(await store.RecordAsync(delivery, renewed, TestContext.Current.CancellationToken));
            Assert.False(await store.ReleaseAsync(renewed, TestContext.Current.CancellationToken));

            DeliveryProgress standing = await store.ProgressAsync(delivery.Id, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The delivery was not written.");

            Assert.Equal((ErasureStatus.AwaitingSubscribers, 0), (standing.Delivery.Status, standing.Delivery.Attempts));
            Assert.Equal(["records"], standing.Delivery.Confirmed);
            Assert.Equal(Noon.AddMinutes(1), standing.ConfirmedAt["records"]);

            Assert.True(await store.RecordAsync(delivery, taken, TestContext.Current.CancellationToken));
            Assert.False(await store.RecordAsync(delivery, taken, TestContext.Current.CancellationToken));
        }

        await using StoreContext after = database.Context();

        Delivery closed = await Store(after, Noon).FindAsync(delivery.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The delivery was not written.");

        Assert.Equal((ErasureStatus.Complete, 1), (closed.Status, closed.Attempts));
        Assert.Equal(["records"], closed.Confirmed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a claim succeeds only where the row's next attempt is due, so
    /// a delivery a pass released and rescheduled is not claimed before that instant, and
    /// one that failed is not claimed at all.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ADeliveryReleasedAndRescheduledIsNotClaimedBeforeItIsDueAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var rescheduled = Delivery.Of(subject, SubjectEventKind.RestrictionChanged, Noon, restricted: true);
        var failed = Delivery.Of(subject, SubjectEventKind.RestrictionChanged, Noon.AddSeconds(1), restricted: true);

        await AddedAsync(rescheduled);
        await AddedAsync(failed);

        rescheduled.Attempted(Noon, TimeSpan.FromMinutes(10), 2.0m, jitter: 1);
        failed.Attempted(Noon.AddSeconds(1), TimeSpan.FromMinutes(10), 2.0m, jitter: 1);
        failed.Fail();

        foreach ((Delivery delivery, DateTimeOffset at) in new[] { (rescheduled, Noon), (failed, Noon.AddSeconds(1)) })
        {
            DeliveryClaim claim = await ClaimedAsync(delivery.Id, at)
                ?? throw new Xunit.Sdk.XunitException("The delivery was not claimed.");

            await using StoreContext recording = database.Context();

            Assert.True(await Store(recording, Noon).RecordAsync(delivery, claim, TestContext.Current.CancellationToken));
        }

        Assert.Null(await ClaimedAsync(rescheduled.Id, Noon.AddMinutes(10) - TimeSpan.FromSeconds(1)));
        Assert.Null(await ClaimedAsync(failed.Id, Noon.AddDays(1)));
        Assert.NotNull(await ClaimedAsync(rescheduled.Id, Noon.AddMinutes(10)));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, DR-016 AC5: a completed erasure the ledger has not confirmed
    /// is claimed by one pass for its line, whatever its last schedule; once the line is
    /// confirmed under the claim and the claim released, no pass claims it again, and
    /// its status and attempts stand as they were. An erasure still outstanding is not
    /// claimed for the line.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ACompletedErasureIsClaimedForItsLineByOnePassAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var completed = Delivery.Of(subject, SubjectEventKind.ErasureRequested, Noon);
        var awaiting = Delivery.Of(subject, SubjectEventKind.ErasureRequested, Noon.AddSeconds(1));

        completed.Attempted(Noon, TimeSpan.FromHours(1), 2.0m, jitter: 1);
        completed.Complete();

        await AddedAsync(completed);
        await AddedAsync(awaiting);

        Assert.Null(await ClaimedForLineAsync(awaiting.Id, Noon.AddSeconds(1)));

        DeliveryClaim?[] claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimedForLineAsync(completed.Id, Noon)));

        DeliveryClaim claim = Assert.Single(claims, one => one is not null)!.Value;

        Assert.True(await ConfirmedAsync(claim, "erasure-ledger", Noon));

        await using (StoreContext releasing = database.Context())
        {
            Assert.True(await Store(releasing, Noon).ReleaseAsync(claim, TestContext.Current.CancellationToken));
        }

        Assert.Null(await ClaimedForLineAsync(completed.Id, Noon.AddDays(1)));

        await using StoreContext reading = database.Context();

        Delivery held = await Store(reading, Noon).FindAsync(completed.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The delivery was not written.");

        Assert.Equal((ErasureStatus.Complete, 1), (held.Status, held.Attempts));
        Assert.Equal(["erasure-ledger"], held.Confirmed);
    }

    /// <summary>
    /// IDN-LIFE-003a, CONV-DESIGN-003 AC6: two operators closing one failed erasure at
    /// once each decide under the lock on its row, so the second finds it closed and
    /// one completion stands.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003a_TwoCompletionsAtOnceCloseTheErasureOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var delivery = Delivery.Of(subject, SubjectEventKind.ErasureRequested, Noon);

        delivery.Fail();

        await using (StoreContext writing = database.Context())
        {
            await Store(writing, Noon).AddAsync(delivery, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        bool[] closed = await Task.WhenAll(ClosedOnceAsync(delivery.Id), ClosedOnceAsync(delivery.Id));

        await using StoreContext reading = database.Context();

        Assert.Equal(1, closed.Count(answer => answer));
        Assert.Equal(
            ErasureStatus.Complete,
            (await Store(reading, Noon).FindAsync(delivery.Id, TestContext.Current.CancellationToken))?.Status);
    }

    private static OutboxStore Store(StoreContext context, DateTimeOffset now) =>
        new(context, new FixedTime(now));

    private async Task AddedAsync(Delivery delivery)
    {
        await using StoreContext writing = database.Context();

        await Store(writing, Noon).AddAsync(delivery, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A claim as a pass takes it: one conditional update, committed on its own.
    private async Task<DeliveryClaim?> ClaimedAsync(DeliveryId delivery, DateTimeOffset now)
    {
        await using StoreContext claiming = database.Context();

        return await Store(claiming, Noon).ClaimAsync(delivery, now, Timeout, TestContext.Current.CancellationToken);
    }

    private async Task<DeliveryClaim?> ClaimedForLineAsync(DeliveryId delivery, DateTimeOffset now)
    {
        await using StoreContext claiming = database.Context();

        return await Store(claiming, Noon).ClaimUnledgeredAsync(delivery, now, Timeout, TestContext.Current.CancellationToken);
    }

    // A confirmation as a pass writes it: in a unit of work of its own, under the claim.
    private async Task<bool> ConfirmedAsync(DeliveryClaim claim, string subscriber, DateTimeOffset at)
    {
        await using StoreContext confirming = database.Context();
        await using var work = new UnitOfWork(confirming);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        bool confirmed = await Store(confirming, Noon).ConfirmAsync(claim, subscriber, at, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return confirmed;
    }

    // A completion as the service makes one: the delivery read before, then again under
    // its row's lock, and closed only where it still stands failed.
    private async Task<bool> ClosedOnceAsync(DeliveryId delivery)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        OutboxStore store = Store(writing, Noon);

        _ = await store.FindAsync(delivery, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        Delivery held = Assert.IsType<Delivery>(
            await store.FindForUpdateAsync(delivery, TestContext.Current.CancellationToken));

        bool failed = held.Status is ErasureStatus.Failed;

        if (failed)
        {
            held.CompleteManually();

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return failed;
    }
}
