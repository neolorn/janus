using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Authentication.Callbacks;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the callbacks a host mounts keep: the provider events claimed, once each, and
/// the correlation references issued, as their hashes (BFF-MACH-002, BFF-MACH-003,
/// INT-GEN-003).
/// </summary>
/// <remarks>
/// One database serves the class, so each test claims and issues values of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class CallbackStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Events = "provider-events";

    private const string Status = "provider-status";

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// BFF-MACH-002 AC3: an event is claimed once, however many times it is delivered,
    /// and the same identifier from another callback is another event.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_002_AC3_AnEventIsClaimedOnceAsync()
    {
        byte[] identifier = Hashed("evt-claimed-once");

        Assert.True(await ClaimedAsync(Events, identifier));
        Assert.False(await ClaimedAsync(Events, identifier));
        Assert.True(await ClaimedAsync(Status, identifier));
    }

    /// <summary>
    /// BFF-MACH-002 AC3: an event whose claim is given back, because the route did not
    /// carry it, is claimed again when the provider delivers it again.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_002_AC3_AnEventGivenBackIsClaimedAgainAsync()
    {
        byte[] identifier = Hashed("evt-given-back");

        Assert.True(await ClaimedAsync(Events, identifier));

        await using (StoreContext writing = database.Context())
        {
            await new CallbackEventStore(writing, new DataConnections(writing))
                .ReleaseAsync(Events, identifier, TestContext.Current.CancellationToken);
        }

        Assert.True(await ClaimedAsync(Events, identifier));
    }

    /// <summary>
    /// INT-GEN-003 and BFF-MACH-003 AC2: a reference is held for the callback it was
    /// issued for and no other, and one never issued is not held.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_AReferenceIsHeldForItsCallbackOnlyAsync()
    {
        byte[] reference = Hashed("reference-held");

        await using (StoreContext writing = database.Context())
        {
            await new CallbackReferenceStore(writing)
                .AddAsync(Status, reference, Noon, TestContext.Current.CancellationToken);

            _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await HeldAsync(Status, reference));
        Assert.False(await HeldAsync(Events, reference));
        Assert.False(await HeldAsync(Status, Hashed("reference-never-issued")));
    }

    /// <summary>
    /// INT-GEN-003: the tables hold a hash, the callback's name and a time, and nothing
    /// a provider sent.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_TheTablesHoldHashesAndTimesAndNothingElseAsync()
    {
        Assert.Equal(["callback", "claimed_at", "identifier"], await ColumnsAsync("callback_events"));
        Assert.Equal(["callback", "issued_at", "reference"], await ColumnsAsync("callback_references"));
    }

    private static byte[] Hashed(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private async Task<bool> ClaimedAsync(string callback, byte[] identifier)
    {
        await using StoreContext writing = database.Context();

        return await new CallbackEventStore(writing, new DataConnections(writing))
            .ClaimAsync(callback, identifier, Noon, TestContext.Current.CancellationToken);
    }

    private async Task<bool> HeldAsync(string callback, byte[] reference)
    {
        await using StoreContext reading = database.Context();

        return await new CallbackReferenceStore(reading)
            .HoldsAsync(callback, reference, TestContext.Current.CancellationToken);
    }

    private async Task<string[]> ColumnsAsync(string table)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = @table
            ORDER BY column_name
            """,
            new { table });

        return [.. columns];
    }
}
