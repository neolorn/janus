using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The offer to attach an account session holds, as the database keeps it (REC-ATT-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RecordOfferStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REC-ATT-001: an offer reads back as it was made, each value proved by its kind and
    /// fingerprints, and the latest proof's offer stands in place of the one before.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ATT_001_AnOfferReadsBackAndTheLatestStandsAsync()
    {
        SessionId session = await SessionAsync();
        ProvenValue phone = Proven(IdentifierKind.Phone);
        ProvenValue email = Proven(IdentifierKind.Email);
        SubjectId first = Subjects.New();
        SubjectId second = Subjects.New();

        await PutAsync(OfferToAttach.Made(session, [phone], [first], Noon, TimeSpan.FromMinutes(15)));
        await PutAsync(OfferToAttach.Made(session, [phone, email], [second, first], Noon.AddMinutes(1), TimeSpan.FromMinutes(15)));

        await using StoreContext reading = database.Context();

        OfferToAttach read = Assert.IsType<OfferToAttach>(
            await new RecordOfferStore(reading).FindAsync(session, TestContext.Current.CancellationToken));

        Assert.Equal(
            [(IdentifierKind.Phone, phone.Fingerprints), (IdentifierKind.Email, email.Fingerprints)],
            read.Proven.Select(value => (value.Kind, value.Fingerprints)));
        Assert.Equal([.. new[] { first, second }.OrderBy(subject => subject.Value)], read.Offered);
        Assert.Equal(Noon.AddMinutes(16), read.ExpiresAt);
    }

    /// <summary>
    /// REC-ATT-001: an offer held under its lock is removed with the attachment, and one
    /// past its life is swept while a live one stays.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ATT_001_AnOfferEndsWhenAttachedOrSweptAsync()
    {
        SessionId attaching = await SessionAsync();
        SessionId lapsing = await SessionAsync();
        SessionId living = await SessionAsync();

        await PutAsync(OfferToAttach.Made(attaching, [Proven(IdentifierKind.Phone)], [], Noon, TimeSpan.FromMinutes(15)));
        await PutAsync(OfferToAttach.Made(lapsing, [Proven(IdentifierKind.Phone)], [], Noon, TimeSpan.FromMinutes(15)));
        await PutAsync(OfferToAttach.Made(living, [Proven(IdentifierKind.Phone)], [], Noon.AddHours(1), TimeSpan.FromMinutes(15)));

        await using (StoreContext ending = database.Context())
        {
            await using IDbContextTransaction transaction = await ending.Database
                .BeginTransactionAsync(TestContext.Current.CancellationToken);
            var store = new RecordOfferStore(ending);

            _ = Assert.IsType<OfferToAttach>(
                await store.FindForUpdateAsync(attaching, TestContext.Current.CancellationToken));
            await store.RemoveAsync(attaching, TestContext.Current.CancellationToken);
            _ = await ending.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext sweeping = database.Context())
        {
            Assert.True(await new RecordOfferStore(sweeping)
                .SweepAsync(Noon.AddMinutes(15), TestContext.Current.CancellationToken) >= 1);
        }

        await using StoreContext reading = database.Context();
        var read = new RecordOfferStore(reading);

        Assert.Null(await read.FindAsync(attaching, TestContext.Current.CancellationToken));
        Assert.Null(await read.FindAsync(lapsing, TestContext.Current.CancellationToken));
        Assert.NotNull(await read.FindAsync(living, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// CONV-DESIGN-003: an offer's row is held only inside the operation's transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task FindForUpdateAsync_OutsideATransaction_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new RecordOfferStore(context).FindForUpdateAsync(
                SessionId.New(TimeProvider.System),
                TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static ProvenValue Proven(IdentifierKind kind) =>
        new(kind, [RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)]);

    private async Task PutAsync(OfferToAttach offer)
    {
        await using StoreContext writing = database.Context();

        await new RecordOfferStore(writing).PutAsync(offer, TestContext.Current.CancellationToken);
        _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The account session an offer is held on, which the offer's row names.
    private async Task<SessionId> SessionAsync()
    {
        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            await _deployment.AccountAsync(Noon),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        await using StoreContext writing = database.Context();

        await new SessionStore(writing, _deployment.Ring, _deployment.Randomness).AddAsync(
            session,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);
        _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }
}
