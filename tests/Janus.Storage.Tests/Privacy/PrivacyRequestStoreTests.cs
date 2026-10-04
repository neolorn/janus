using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Requests;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Migrations;
using Janus.Storage.Privacy.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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

        written.ReceiptAdmitted();

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading, new DataConnections(reading))
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Equal(subject, held.Subject);
        Assert.Equal(PrivacyRequestType.Erasure, held.Type);
        Assert.Equal("please act on this", held.Detail);
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
    /// PRIV-RIGHT-002 AC1: a request whose receipt a sending restriction refused holds
    /// no receipt-sent timestamp in its row, and reads back with none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ARequestWhoseReceiptWasRefusedReadsBackWithNoneAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Restriction);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading, new DataConnections(reading))
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Null(held.ReceiptSentAt);
        Assert.Equal(Noon, held.CreatedAt);
        Assert.True(held.Open);
        Assert.Null(await reading.PrivacyRequests
            .Where(row => row.Id == written.Id)
            .Select(row => row.ReceiptSentAt)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC1 (D-186): a request queued before the receipt's instant was
    /// kept reads back, once the migration has run, the receipt at creation it was
    /// queued with.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ARequestQueuedBeforeTheReceiptWasKeptReadsBackItsReceiptAsync()
    {
        string moved = await database.CreateDatabaseAsync("request_receipt");
        string keeping;

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            keeping = declared.Single(migration =>
                migration.EndsWith("_" + nameof(KeepWhetherARequestsReceiptWasSent), StringComparison.Ordinal));

            await migrating.GetService<IMigrator>().MigrateAsync(
                declared[Array.IndexOf(declared, keeping) - 1],
                TestContext.Current.CancellationToken);
        }

        var values = new
        {
            id = Guid.CreateVersion7(),
            subject = Subjects.New().Value,
            received = new DateOnly(2026, 9, 18),
            at = Noon,
            due = Clock.Due,
            warn = Clock.WarnAt,
            escalate = Clock.EscalateAt,
        };

        await using (var connection = new NpgsqlConnection(moved))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.accounts (subject, state, created_at)
                VALUES (@subject, 'active', @at);
                INSERT INTO identity.privacy_requests
                    (id, subject, type, received_at, created_at, decision_due, warn_at, escalate_at, status)
                VALUES
                    (@id, @subject, 'restriction', @received, @at, @due, @warn, @escalate, 'open');
                """,
                values);
        }

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(keeping, TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = DatabaseFixture.Context(moved);

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading, new DataConnections(reading))
                .FindAsync(new PrivacyRequestId(values.id), TestContext.Current.CancellationToken));

        Assert.Equal(Noon, held.CreatedAt);
        Assert.Equal(Noon, held.ReceiptSentAt);
    }

    /// <summary>
    /// API-CONV-002 AC3: a request entered with no detail holds none in its row, not an
    /// empty text, and reads back with none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AnEntryWithNoDetailIsStoredAsNoneAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Erasure, detail: null);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading, new DataConnections(reading))
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Null(held.Detail);
        Assert.Null(await reading.PrivacyRequests
            .Where(row => row.Id == written.Id)
            .Select(row => row.Detail)
            .SingleAsync(TestContext.Current.CancellationToken));
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
            await new PrivacyRequestStore(reading, new DataConnections(reading))
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

        IReadOnlyList<QueuedRequest> reached = await new PrivacyRequestStore(reading, new DataConnections(reading))
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
        var store = new PrivacyRequestStore(reading, new DataConnections(reading));

        Assert.True(await store.OpenAsync(
            subject,
            PrivacyRequestType.Restriction,
            TestContext.Current.CancellationToken));
        Assert.False(await store.OpenAsync(
            subject,
            PrivacyRequestType.Rectification,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC5, CONV-DESIGN-003 AC6: two decisions on one request at once are
    /// each made under the lock on its row, so the second finds the first and one
    /// decision stands.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_TwoDecisionsAtOnceDecideOnceAsync()
    {
        SubjectId subject = await RegisteredAsync();
        QueuedRequest written = Entered(subject, PrivacyRequestType.Erasure);

        await WritingAsync(async store => await store.AddAsync(
            written,
            TestContext.Current.CancellationToken));

        bool[] decided = await Task.WhenAll(
            DecidedOnceAsync(written.Id, request => request.Fulfil(Noon.AddDays(1))),
            DecidedOnceAsync(written.Id, request => request.DeemRefusedByLapse(Noon.AddDays(9))));

        await using StoreContext reading = database.Context();

        QueuedRequest after = Assert.IsType<QueuedRequest>(
            await new PrivacyRequestStore(reading, new DataConnections(reading))
                .FindAsync(written.Id, TestContext.Current.CancellationToken));

        Assert.Equal(1, decided.Count(answer => answer));
        Assert.False(after.Open);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC1, CONV-DESIGN-003 AC6: two requests of one type at once are
    /// each queued with the subject's requests of it held, so the second finds the
    /// first open and one is queued.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_TwoRequestsOfATypeAtOnceQueueOneAsync()
    {
        SubjectId subject = await RegisteredAsync();

        bool[] queued = await Task.WhenAll(
            QueuedOnceAsync(Entered(subject, PrivacyRequestType.Restriction)),
            QueuedOnceAsync(Entered(subject, PrivacyRequestType.Restriction)));

        await using StoreContext reading = database.Context();

        Assert.Equal(1, queued.Count(answer => answer));
        Assert.Equal(
            1,
            (await new PrivacyRequestStore(reading, new DataConnections(reading))
                .AllAsync(TestContext.Current.CancellationToken))
                .Count(request => request.Subject == subject));
    }

    private static QueuedRequest Entered(
        SubjectId subject,
        PrivacyRequestType type,
        string? detail = "please act on this") =>
        QueuedRequest.Entered(
            new PrivacyRequestEntry(
                subject,
                type,
                detail,
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

    // A request queued as the service queues one: the subject's requests of the type
    // held, and the request written only where none stands open.
    private async Task<bool> QueuedOnceAsync(QueuedRequest request)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var store = new PrivacyRequestStore(writing, new DataConnections(writing));

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        await store.HoldAsync(request.Subject, request.Type, TestContext.Current.CancellationToken);

        bool fresh = !await store.OpenAsync(request.Subject, request.Type, TestContext.Current.CancellationToken);

        if (fresh)
        {
            await store.AddAsync(request, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return fresh;
    }

    // A decision as the service makes one: the request read before, then again under
    // its row's lock, and decided only where it still stands open.
    private async Task<bool> DecidedOnceAsync(PrivacyRequestId request, Action<QueuedRequest> decide)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var store = new PrivacyRequestStore(writing, new DataConnections(writing));

        _ = await store.FindAsync(request, TestContext.Current.CancellationToken);
        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        QueuedRequest held = Assert.IsType<QueuedRequest>(
            await store.FindForUpdateAsync(request, TestContext.Current.CancellationToken));

        bool open = held.Open;

        if (open)
        {
            decide(held);

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return open;
    }

    private async Task WritingAsync(Func<PrivacyRequestStore, Task> write)
    {
        await using StoreContext writing = database.Context();

        await write(new PrivacyRequestStore(writing, new DataConnections(writing)));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
