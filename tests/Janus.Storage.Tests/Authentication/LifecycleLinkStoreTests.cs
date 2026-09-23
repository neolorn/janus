using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Storage.Authentication.Accounts;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The link an account's own deactivation or deletion notice carried, as the
/// <c>lifecycle_links</c> table holds it: one per account, by fingerprint
/// (IDN-LIFE-013, IDN-LIFE-014).
/// </summary>
[Trait("kind", "integration")]
public sealed class LifecycleLinkStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-LIFE-013: the row carries what the token hashes to and nothing the token
    /// could be read back out of, so a dump stands no account back up.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_TheTableHoldsNoTokenACallerCouldPresentAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_randomness);

        await WrittenAsync(LifecycleLink.Issued(
            subject,
            LifecycleLinkKind.Reactivation,
            token,
            Noon));

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'lifecycle_links'
            """);

        Assert.Equal(["issued_at", "kind", "subject", "token"], columns.Order());

        byte[] held = await connection.QuerySingleAsync<byte[]>(
            "SELECT token FROM identity.lifecycle_links WHERE subject = @subject",
            new { subject = subject.Value });

        Assert.Equal(token.Fingerprint(), held);
    }

    /// <summary>
    /// IDN-LIFE-013: the link is found by the fingerprint of what the caller
    /// presents, and carries which state it ends.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_TheLinkIsFoundByWhatTheCallerPresentsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_randomness);

        await WrittenAsync(LifecycleLink.Issued(
            subject,
            LifecycleLinkKind.DeletionCancellation,
            token,
            Noon));

        await using StoreContext reading = database.Context();

        LifecycleLink found = Assert.IsType<LifecycleLink>(
            await new LifecycleLinkStore(reading).FindAsync(
                OpaqueToken.Of(token.Value).Fingerprint(),
                TestContext.Current.CancellationToken));

        Assert.Equal(subject, found.Subject);
        Assert.Equal(LifecycleLinkKind.DeletionCancellation, found.Kind);
        Assert.Equal(Noon, found.IssuedAt);
    }

    /// <summary>
    /// IDN-LIFE-013: an account holds one link at a time, so a second issue leaves
    /// the first presenting nothing.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AnAccountHoldsOneLinkAtATimeAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var first = OpaqueToken.Draw(_randomness);
        var second = OpaqueToken.Draw(_randomness);

        await WrittenAsync(LifecycleLink.Issued(subject, LifecycleLinkKind.Reactivation, first, Noon));

        await WrittenAsync(LifecycleLink.Issued(
            subject,
            LifecycleLinkKind.DeletionCancellation,
            second,
            Noon.AddHours(1)));

        await using StoreContext reading = database.Context();
        var store = new LifecycleLinkStore(reading);

        Assert.Null(await store.FindAsync(
            first.Fingerprint(),
            TestContext.Current.CancellationToken));

        Assert.NotNull(await store.FindAsync(
            second.Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-013: the link the reactivation spent is gone, so what stood the
    /// account up once stands nothing up again.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_TheSpentLinkLeavesNoRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_randomness);

        await WrittenAsync(LifecycleLink.Issued(subject, LifecycleLinkKind.Reactivation, token, Noon));

        await using (StoreContext removing = database.Context())
        {
            await new LifecycleLinkStore(removing).RemoveAsync(
                subject,
                TestContext.Current.CancellationToken);

            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await new LifecycleLinkStore(reading).FindAsync(
            token.Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _deployment.Dispose();
        _randomness.Dispose();
    }

    private async Task WrittenAsync(LifecycleLink link)
    {
        await using StoreContext writing = database.Context();

        await new LifecycleLinkStore(writing).ReplaceAsync(link, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
