using System;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Authentication.Sessions;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a collection session stages, as the database keeps it (REC-COL-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class CollectionSessionStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Number = "+441632960011";

    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REC-COL-001: what a session staged reads back as it was written: the age answer,
    /// each contact and whether its code verified it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_001_ASessionReadsBackAsItWasStagedAsync()
    {
        CollectionSession staged = Opened();

        staged.AnswerAge(adult: true, new DateOnly(1990, 1, 1), group: null, Noon);
        staged.Stage(IdentifierKind.Phone, Number);
        staged.Verify(IdentifierKind.Phone, Noon.AddMinutes(1));
        staged.Stage(IdentifierKind.Email, "person@example.test");

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(staged, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        CollectionSession read = Assert.IsType<CollectionSession>(
            await Store(reading).FindAsync(staged.Id, TestContext.Current.CancellationToken));

        Assert.True(read.AsksAge);
        Assert.Equal("en", read.Language);
        Assert.Equal(staged.ExpiresAt, read.ExpiresAt);
        Assert.True(read.AdultAffirmed);
        Assert.Equal(new DateOnly(1990, 1, 1), read.DateOfBirth);
        Assert.Equal(Noon, read.AnsweredAgeAt);
        Assert.False(read.AgeRefused);
        Assert.Equal(staged.Contacts, read.Contacts);
    }

    /// <summary>
    /// REC-COL-001 AC5: an expired session is swept and leaves nothing behind, and the
    /// browser that carried it carries nothing in its place.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_001_AC5_AnExpiredSessionIsSweptAndReleasesItsBrowserAsync()
    {
        CollectionSession lapsed = Opened();
        CollectionSession live = Opened();
        var secret = OpaqueToken.Of("the-cookie-of-a-collecting-browser");
        var contact = PreAuthentication.Issue(secret, OpaqueToken.Of("the-csrf-of-a-collecting-browser"), Noon, TimeSpan.FromHours(2));

        contact.Carry(lapsed.Id, lapsed.ExpiresAt);
        lapsed.Stage(IdentifierKind.Phone, Number);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(lapsed, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PreAuthenticationStore(writing, _deployment.DataKey(writing))
                .AddAsync(contact, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(live, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext sweeping = database.Context())
        {
            Assert.True(await Store(sweeping).SweepAsync(lapsed.ExpiresAt, TestContext.Current.CancellationToken) >= 1);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(lapsed.Id, TestContext.Current.CancellationToken));
        Assert.Null((await new PreAuthenticationStore(reading, _deployment.DataKey(reading))
            .FindAsync(secret.Fingerprint(), TestContext.Current.CancellationToken))!.Collection);
    }

    /// <summary>
    /// REC-COL-001, OPS-SEC-001: a dump of the table holds no form of a staged contact,
    /// and the value moved onto another session's row does not open there
    /// (PRIV-RIGHT-005a AC18).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_001_ADumpHoldsNoFormOfAStagedContactAsync()
    {
        CollectionSession moved = Opened();
        CollectionSession other = Opened();

        moved.Stage(IdentifierKind.Phone, Number);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(moved, TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(other, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            byte[] dumped = await connection.ExecuteScalarAsync<byte[]>(
                "SELECT enc_session FROM identity.collection_sessions WHERE id = @id",
                new { id = moved.Id.Value }) ?? [];

            Assert.Equal(-1, dumped.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Number)));

            await connection.ExecuteAsync(
                """
                UPDATE identity.collection_sessions AS other
                SET wrapped_key = moved.wrapped_key, enc_session = moved.enc_session
                FROM identity.collection_sessions AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await Store(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
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
            await Store(context).FindForUpdateAsync(
                CollectionSessionId.New(TimeProvider.System),
                TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static CollectionSession Opened() =>
        CollectionSession.Open(
            CollectionSessionId.New(TimeProvider.System),
            asksAge: true,
            "en",
            Noon,
            TimeSpan.FromHours(1));

    private CollectionSessionStore Store(StoreContext context) =>
        new(context, _deployment.DataKey(context), _deployment.Randomness);
}
