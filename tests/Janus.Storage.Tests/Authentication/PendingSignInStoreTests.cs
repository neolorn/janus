using System;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Storage.Authentication.SignIn;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The sign-in links and codes that have gone out, over the <c>signin_links</c> table
/// (AUTH-FACT-003, AUTH-FACT-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class PendingSignInStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-FACT-004 AC4: a pending sign-in held for a try is not read by a second try
    /// until the first commits, and the second then decides on what the first left.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC4_APendingSignInHeldForATryIsReadByTheNextOnlyAfterItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(token, subject, Factor.EmailLink, null, "123456", null, null, Noon, TimeSpan.FromMinutes(15)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext first = database.Context();
        await using var firstWork = new UnitOfWork(first);
        await using StoreContext second = database.Context();
        await using var secondWork = new UnitOfWork(second);

        _ = await firstWork.BeginAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await Store(first).FindForUpdateAsync(token.Fingerprint(), TestContext.Current.CancellationToken));

        _ = await secondWork.BeginAsync(TestContext.Current.CancellationToken);
        Task<PendingSignIn?> waiting = Store(second)
            .FindForUpdateAsync(token.Fingerprint(), TestContext.Current.CancellationToken)
            .AsTask();

        Assert.NotSame(
            waiting,
            await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken)));

        await Store(first).RemoveAsync(token.Fingerprint(), TestContext.Current.CancellationToken);
        _ = await firstWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(await waiting);
    }

    /// <summary>
    /// AUTH-FACT-002 AC4: a second step's code is read back bound to the sign-in or
    /// step-up it was issued for, and answers that one and no other.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC4_ASecondStepCodeIsReadBackBoundToItsChallengeAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);
        byte[] challenge = OpaqueToken.Draw(_deployment.Randomness).Fingerprint();

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).ReplaceAsync(
                PendingSignIn.Issue(token, subject, Factor.PhoneCode, null, "123456", null, challenge, Noon, TimeSpan.FromMinutes(10)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        PendingSignIn? read = await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.True(read.Answers(challenge));
        Assert.False(read.Answers(OpaqueToken.Draw(_deployment.Randomness).Fingerprint()));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private PendingSignInStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness, new DataConnections(context));
}
