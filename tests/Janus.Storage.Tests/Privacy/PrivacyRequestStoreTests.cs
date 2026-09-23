using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Requests;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.Requests;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the table holds of a data subject request: the three instants the clock gave
/// it, the decision, and the row that persists whatever the decision was
/// (PRIV-RIGHT-001, PRIV-RIGHT-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class PrivacyRequestStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly Deadline Clock = new(
        Noon.AddDays(8).AddHours(11).AddMinutes(59),
        Noon.AddDays(4),
        Noon.AddDays(8));

    /// <summary>
    /// PRIV-RIGHT-002 AC1: a request reads back carrying every field it was written
    /// with, the deadline and the receipt among them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ARequestReadsBackEveryFieldItWasWrittenWithAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Erasure);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading)
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Equal(subject, held.Subject);
        Assert.Equal(PrivacyRequestType.Erasure, held.Type);
        Assert.Equal(new DateOnly(2026, 9, 18), held.ReceivedAt);
        Assert.Equal(Noon, held.CreatedAt);
        Assert.Equal(Noon, held.ReceiptSentAt);
        Assert.Equal(Clock.Due, held.DecisionDue);
        Assert.Equal(Clock.WarnAt, held.WarnAt);
        Assert.Equal(Clock.EscalateAt, held.EscalateAt);
        Assert.Equal("letter", held.Channel);
        Assert.Equal("national identity card seen", held.IdentityConfirmation);
        Assert.True(held.Open);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC4: a decided request keeps its row, the lapse among the
    /// decisions, so the record persists.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC4_ADecidedRequestKeepsItsRowAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Erasure);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await WritingAsync(async store =>
        {
            QueuedRequest held = Assert.IsType<QueuedRequest>(
                await store.FindAsync(written.Id, TestContext.Current.CancellationToken));

            held.DeemRefusedByLapse(Noon.AddDays(9));

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        });

        await using StoreContext reading = database.Context();

        QueuedRequest after = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading)
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Equal(PrivacyRequestStatus.DeemedRefusedByLapse, after.Status);
        Assert.Equal(Noon.AddDays(9), after.DecidedAt);
        Assert.False(after.Open);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC2, AC5: the sweep reads the open requests the clock has
    /// reached, and a decided one is not among them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_ADecidedRequestIsNotReachedByTheSweepAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest open = Entered(subject, PrivacyRequestType.Restriction);
        QueuedRequest decided = Entered(subject, PrivacyRequestType.Rectification);

        decided.Fulfil(Noon.AddDays(1));

        await WritingAsync(async store =>
        {
            await store.AddAsync(open, TestContext.Current.CancellationToken);
            await store.AddAsync(decided, TestContext.Current.CancellationToken);
        });

        await WritingAsync(async store =>
            await store.RecordAsync(decided, TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        IReadOnlyList<QueuedRequest> reached = await new PrivacyRequestStore(reading)
            .ReachedAsync(Clock.EscalateAt, TestContext.Current.CancellationToken);

        Assert.Contains(reached, request => request.Id == open.Id);
        Assert.DoesNotContain(reached, request => request.Id == decided.Id);
    }

    /// <summary>
    /// PRIV-RIGHT-001: a second request of a type the subject already has open is
    /// read as a duplicate, and a decided one of the same type is not.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_AnOpenRequestOfATypeIsReadBackAsADuplicateAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Restriction);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();
        var store = new PrivacyRequestStore(reading);

        Assert.True(await store.OpenAsync(
            subject,
            PrivacyRequestType.Restriction,
            TestContext.Current.CancellationToken));
        Assert.False(await store.OpenAsync(
            subject,
            PrivacyRequestType.Rectification,
            TestContext.Current.CancellationToken));
    }

    private static QueuedRequest Entered(SubjectId subject, PrivacyRequestType type) =>
        QueuedRequest.Entered(
            new PrivacyRequestEntry(
                subject,
                type,
                "please act on this",
                new DateOnly(2026, 9, 18),
                "letter",
                "national identity card seen"),
            Noon,
            Clock);

    private async Task<SubjectId> RegisteredAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();

        await new AccountStore(writing)
            .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private async Task WritingAsync(Func<PrivacyRequestStore, Task> write)
    {
        await using StoreContext writing = database.Context();

        await write(new PrivacyRequestStore(writing));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
