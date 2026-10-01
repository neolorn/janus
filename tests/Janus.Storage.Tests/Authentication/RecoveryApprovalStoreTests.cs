using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Authentication.Recovery;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The approvals the day limits on a recovery count, over the <c>recovery_approvals</c>
/// table (AUTH-RECOV-002, BFF-ABUSE-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class RecoveryApprovalStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Channel = "person@example.test";

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// BFF-ABUSE-001 AC2: what an account has drawn and what an approver has given are
    /// read as the instants each approval was given, earliest first whatever order they
    /// were written in, and one given at the instant the window opens after has left
    /// it, so the earliest counted says when a cap lifts.
    /// </summary>
    [Fact]
    public async Task BFF_ABUSE_001_AC2_TheApprovalsCountedAreReadEarliestFirstAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        SubjectId approver = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            RecoveryApprovalStore store = Store(writing);

            await store.AddAsync(
                new RecoveryApproval(subject, approver, Channel, Noon.AddHours(2)),
                TestContext.Current.CancellationToken);
            await store.AddAsync(
                new RecoveryApproval(subject, approver, Channel, Noon),
                TestContext.Current.CancellationToken);
            await store.AddAsync(
                new RecoveryApproval(other, approver, Channel, Noon.AddHours(3)),
                TestContext.Current.CancellationToken);
            await store.AddAsync(
                new RecoveryApproval(subject, approver, Channel, Noon.AddHours(1)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RecoveryApprovalStore read = Store(reading);

        Assert.Equal(
            [Noon.AddHours(1), Noon.AddHours(2)],
            await read.ForAsync(subject, Noon, TestContext.Current.CancellationToken));
        Assert.Equal(
            [Noon.AddHours(1), Noon.AddHours(2), Noon.AddHours(3)],
            await read.ByAsync(approver, Noon, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-002, CONV-DESIGN-003 AC6: two approvals for one account at once, with
    /// room for one more under the account's day limit, are counted under the hold, so
    /// the second waits for the first, finds the limit reached, and one is given.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_002_ApprovalsAtOnceAreCountedOneAfterAnotherAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId first = await _deployment.AccountAsync(Noon);
        SubjectId second = await _deployment.AccountAsync(Noon);

        bool[] given = await Task.WhenAll(GivenAsync(subject, first), GivenAsync(subject, second));

        await using StoreContext reading = database.Context();

        Assert.Equal(1, given.Count(answer => answer));
        Assert.Single(await Store(reading).ForAsync(subject, Noon, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each approval is its own request, counting the account's approvals against a
    // limit of one under the hold as the recovery service does.
    private async Task<bool> GivenAsync(SubjectId subject, SubjectId approver)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        RecoveryApprovalStore store = Store(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        await store.HoldAsync(TestContext.Current.CancellationToken);

        bool admitted = (await store.ForAsync(subject, Noon, TestContext.Current.CancellationToken)).Count < 1;

        if (admitted)
        {
            await store.AddAsync(
                new RecoveryApproval(subject, approver, Channel, Noon.AddHours(1)),
                TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return admitted;
    }

    private RecoveryApprovalStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness, new DataConnections(context));
}
