using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a record access session holds, as the database keeps it (REC-ACC-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RecordAccessSessionStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REC-ACC-001: a session reads back as it was written, and once opened it holds when
    /// it opened, its new end and every record it reached.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_001_ASessionReadsBackAsItWasWrittenAsync()
    {
        RecordAccessSession asked = Asked();
        SubjectId first = Subjects.New();
        SubjectId second = Subjects.New();

        await using (StoreContext writing = database.Context())
        {
            await new RecordAccessSessionStore(writing).AddAsync(asked, TestContext.Current.CancellationToken);
            _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext opening = database.Context())
        {
            await using IDbContextTransaction transaction = await opening.Database
                .BeginTransactionAsync(TestContext.Current.CancellationToken);
            var store = new RecordAccessSessionStore(opening);

            RecordAccessSession held = Assert.IsType<RecordAccessSession>(
                await store.FindForUpdateAsync(asked.Id, TestContext.Current.CancellationToken));

            held.Open(Noon.AddMinutes(2), TimeSpan.FromMinutes(15));
            _ = held.Reach([first, second]);
            await store.RecordAsync(held, TestContext.Current.CancellationToken);
            _ = await opening.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        RecordAccessSession read = Assert.IsType<RecordAccessSession>(
            await new RecordAccessSessionStore(reading).FindAsync(asked.Id, TestContext.Current.CancellationToken));

        Assert.Equal(IdentifierKind.Phone, read.Kind);
        Assert.Equal(asked.Fingerprints, read.Fingerprints);
        Assert.Equal(asked.Throttled, read.Throttled);
        Assert.Equal(Noon, read.CreatedAt);
        Assert.True(read.IsOpen);
        Assert.Equal(Noon.AddMinutes(2), read.OpenedAt);
        Assert.Equal(Noon.AddMinutes(17), read.ExpiresAt);
        Assert.Equal(
            new[] { first, second }.OrderBy(subject => subject.Value),
            read.Reached.OrderBy(subject => subject.Value));
    }

    /// <summary>
    /// REC-ACC-001 AC4: a session past its end is swept and leaves nothing behind, and the
    /// browser that carried it carries nothing in its place.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_001_AC4_AnExpiredSessionIsSweptAndReleasesItsBrowserAsync()
    {
        RecordAccessSession lapsed = Asked();
        RecordAccessSession live = Asked(Noon.AddHours(1));
        var secret = OpaqueToken.Of("the-cookie-of-a-browser-reaching-records");
        var contact = PreAuthentication.Issue(secret, OpaqueToken.Of("the-csrf-of-a-browser-reaching-records"), Noon, TimeSpan.FromHours(2));

        contact.Carry(lapsed.Id, lapsed.ExpiresAt);

        await using (StoreContext writing = database.Context())
        {
            var store = new RecordAccessSessionStore(writing);

            await store.AddAsync(lapsed, TestContext.Current.CancellationToken);
            await store.AddAsync(live, TestContext.Current.CancellationToken);
            _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PreAuthenticationStore(writing, _deployment.DataKey(writing))
                .AddAsync(contact, TestContext.Current.CancellationToken);
            _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext sweeping = database.Context())
        {
            Assert.True(await new RecordAccessSessionStore(sweeping)
                .SweepAsync(lapsed.ExpiresAt, TestContext.Current.CancellationToken) >= 1);
        }

        await using StoreContext reading = database.Context();
        var read = new RecordAccessSessionStore(reading);

        Assert.Null(await read.FindAsync(lapsed.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await read.FindAsync(live.Id, TestContext.Current.CancellationToken));
        Assert.Null((await new PreAuthenticationStore(reading, _deployment.DataKey(reading))
            .FindAsync(secret.Fingerprint(), TestContext.Current.CancellationToken))!.Access);
    }

    /// <summary>
    /// CONV-DESIGN-003: a session's row is held only inside the operation's transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task FindForUpdateAsync_OutsideATransaction_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new RecordAccessSessionStore(context).FindForUpdateAsync(
                RecordAccessSessionId.New(TimeProvider.System),
                TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static RecordAccessSession Asked() => Asked(Noon);

    private static RecordAccessSession Asked(DateTimeOffset at) =>
        RecordAccessSession.Ask(
            RecordAccessSessionId.New(TimeProvider.System),
            IdentifierKind.Phone,
            [RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)],
            RandomNumberGenerator.GetBytes(32),
            at,
            TimeSpan.FromMinutes(10));
}
