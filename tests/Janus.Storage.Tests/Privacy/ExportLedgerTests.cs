using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Privacy.Exports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the <c>privacy_exports</c> table answers: how many exports one account has
/// taken inside a window, and when the oldest of them was (PRIV-RIGHT-003, D-086).
/// </summary>
[Trait("kind", "integration")]
public sealed class ExportLedgerTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// PRIV-RIGHT-003: the exports inside the window come back oldest first, and one
    /// that has fallen out of it does not come back at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheExportsInsideTheWindowComeBackOldestFirstAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await CountedAsync(subject, Noon - TimeSpan.FromDays(2));
        await CountedAsync(subject, Noon - TimeSpan.FromHours(3));
        await CountedAsync(subject, Noon - TimeSpan.FromHours(9));

        await using JanusDbContext reading = database.Context();

        IReadOnlyList<DateTimeOffset> taken = await new ExportLedger(reading).SinceAsync(
            subject,
            Noon - TimeSpan.FromDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [Noon - TimeSpan.FromHours(9), Noon - TimeSpan.FromHours(3)],
            taken);
    }

    /// <summary>
    /// PRIV-RIGHT-003: the count is one account's, so another account's exports are
    /// not among them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnotherAccountsExportsAreNotCountedAsync()
    {
        SubjectId ahmed = await _deployment.AccountAsync(Noon);
        SubjectId noura = await _deployment.AccountAsync(Noon);

        await CountedAsync(ahmed, Noon);
        await CountedAsync(noura, Noon);
        await CountedAsync(noura, Noon + TimeSpan.FromMinutes(1));

        await using JanusDbContext reading = database.Context();

        Assert.Equal(
            [Noon],
            await new ExportLedger(reading).SinceAsync(
                ahmed,
                Noon - TimeSpan.FromDays(1),
                TestContext.Current.CancellationToken));
    }

    private async Task CountedAsync(SubjectId subject, DateTimeOffset at)
    {
        await using JanusDbContext writing = database.Context();

        await new ExportLedger(writing).RecordAsync(subject, at, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
