using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Identifiers;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The verifications a live account has outstanding, as the database keeps them
/// (REG-IDENT-004, REG-IDENT-007).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class PendingVerificationStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-IDENT-004, CONV-DESIGN-003 AC6: wrong codes presented at once are each
    /// decided on the verification's row under its lock, so every one is counted and
    /// none is written over another.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_WrongCodesAtOnceAreAllCountedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var staged = IdentifierId.New(TimeProvider.System);
        var identity = StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test");

        identity.Sent(
            RandomNumberGenerator.GetBytes(Fingerprint.Length),
            RandomNumberGenerator.GetBytes(Fingerprint.Length),
            Noon.AddMinutes(10));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                PendingVerification.ToAdd(subject, browser: null, identity, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(MissedAsync(staged), MissedAsync(staged), MissedAsync(staged));

        await using StoreContext reading = database.Context();
        PendingVerification read = Assert.IsType<PendingVerification>(
            await Store(reading).FindAsync(staged, TestContext.Current.CancellationToken));

        Assert.Equal(3, read.Staged.WrongAttempts);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each wrong code is its own request, read before its transaction and again under
    // the lock, as the identifier service does.
    private async Task MissedAsync(IdentifierId staged)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        PendingVerificationStore store = Store(context);

        _ = await store.FindAsync(staged, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        PendingVerification held = Assert.IsType<PendingVerification>(
            await store.FindForUpdateAsync(staged, TestContext.Current.CancellationToken));

        held.Staged.Missed(cap: 5);

        await store.RecordAsync(held, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }

    private PendingVerificationStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);
}
