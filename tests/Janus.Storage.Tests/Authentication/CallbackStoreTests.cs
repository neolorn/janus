using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Callbacks;
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

    private static readonly TimeSpan Timeout =
        Janus.Core.Configuration.Settings.IntegrationCallbackClaimTimeout.Default;

    /// <summary>
    /// BFF-MACH-002 AC3: an event is claimed once, however many times it is delivered:
    /// a delivery meeting the claim while it is unsettled finds it in progress, and one
    /// meeting it settled finds it settled however late it comes. The same identifier
    /// from another callback is another event.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_002_AC3_AnEventIsClaimedOnceAsync()
    {
        byte[] identifier = Hashed("evt-claimed-once");

        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Events, identifier, Noon));
        Assert.Equal(CallbackClaim.InProgress, await ClaimedAsync(Events, identifier, Noon));

        await SettledAsync(Events, identifier, Noon);

        Assert.Equal(CallbackClaim.Settled, await ClaimedAsync(Events, identifier, Noon.AddDays(1)));
        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Status, identifier, Noon));
    }

    /// <summary>
    /// BFF-MACH-002 AC3: an event whose claim is given back, because the route did not
    /// carry it, is claimed again when the provider delivers it again.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_002_AC3_AnEventGivenBackIsClaimedAgainAsync()
    {
        byte[] identifier = Hashed("evt-given-back");

        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Events, identifier, Noon));

        await ReleasedAsync(Events, identifier, Noon);

        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Events, identifier, Noon));
    }

    /// <summary>
    /// BFF-MACH-002 AC3: a claim left unsettled stands against every delivery of its
    /// event for <c>integration.callback.claimtimeout</c>, five minutes by default, and
    /// a delivery once that has passed takes it over. The delivery overtaken can then
    /// neither give the claim back nor settle it; the one that took it over settles it.
    /// </summary>
    [Fact]
    public async Task BFF_MACH_002_AC3_AnUnsettledClaimIsTakenOverOnlyAfterFiveMinutesAsync()
    {
        byte[] identifier = Hashed("evt-taken-over");
        DateTimeOffset late = Noon + Timeout;

        Assert.Equal(TimeSpan.FromMinutes(5), Timeout);
        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Events, identifier, Noon));
        Assert.Equal(CallbackClaim.InProgress, await ClaimedAsync(Events, identifier, late.AddMilliseconds(-1)));
        Assert.Equal(CallbackClaim.Taken, await ClaimedAsync(Events, identifier, late));

        await ReleasedAsync(Events, identifier, Noon);
        await SettledAsync(Events, identifier, Noon);

        Assert.Equal(CallbackClaim.InProgress, await ClaimedAsync(Events, identifier, late));

        await SettledAsync(Events, identifier, late);

        Assert.Equal(CallbackClaim.Settled, await ClaimedAsync(Events, identifier, late + Timeout));
    }

    /// <summary>
    /// IDN-LIFE-012a and BFF-MACH-002 AC3: a provider's security event is claimed
    /// settled in the transaction its work runs in, so it is carried once, and a
    /// delivery that would claim it as a host's callback finds it settled.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_AProviderEventIsClaimedSettledOnceAsync()
    {
        byte[] identifier = Hashed("evt-provider");

        Assert.True(await CarriedAsync(Events, identifier));
        Assert.False(await CarriedAsync(Events, identifier));
        Assert.Equal(CallbackClaim.Settled, await ClaimedAsync(Events, identifier, Noon + Timeout));
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
    /// INT-GEN-003: the tables hold a hash, the callback's name and times, and nothing
    /// a provider sent.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_TheTablesHoldHashesAndTimesAndNothingElseAsync()
    {
        Assert.Equal(["callback", "claimed_at", "identifier", "settled_at"], await ColumnsAsync("callback_events"));
        Assert.Equal(["callback", "issued_at", "reference"], await ColumnsAsync("callback_references"));
    }

    private static byte[] Hashed(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    // Each step in a context of its own, which commits as it goes, as each delivery's does.
    private async Task<CallbackClaim> ClaimedAsync(string callback, byte[] identifier, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();

        return await new CallbackEventStore(writing, new DataConnections(writing))
            .ClaimAsync(callback, identifier, at, at - Timeout, TestContext.Current.CancellationToken);
    }

    private async Task<bool> CarriedAsync(string callback, byte[] identifier)
    {
        await using StoreContext writing = database.Context();

        return await new CallbackEventStore(writing, new DataConnections(writing))
            .CarryAsync(callback, identifier, Noon, TestContext.Current.CancellationToken);
    }

    private async Task SettledAsync(string callback, byte[] identifier, DateTimeOffset claimed)
    {
        await using StoreContext writing = database.Context();

        await new CallbackEventStore(writing, new DataConnections(writing))
            .SettleAsync(callback, identifier, claimed, claimed.AddMinutes(1), TestContext.Current.CancellationToken);
    }

    private async Task ReleasedAsync(string callback, byte[] identifier, DateTimeOffset claimed)
    {
        await using StoreContext writing = database.Context();

        await new CallbackEventStore(writing, new DataConnections(writing))
            .ReleaseAsync(callback, identifier, claimed, TestContext.Current.CancellationToken);
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
