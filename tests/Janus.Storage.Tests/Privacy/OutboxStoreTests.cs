using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;
using Janus.Storage.Privacy.Outbox;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The takedown's delivery on the outbox, and what the takedown screen reads of it
/// (IDN-LIFE-003, IDN-LIFE-003a).
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
            latest.Confirm("orders");

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
        Assert.Equal(confirmedAt, Assert.Contains("orders", progress.ConfirmedAt));
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

    private static OutboxStore Store(StoreContext context, DateTimeOffset now) =>
        new(context, new FixedTime(now));
}
