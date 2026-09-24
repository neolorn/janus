using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Storage.Authentication.Sending;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the sending ledger keeps: a keyed hash of the restriction key and the times
/// counted against it, for as long as any of them decides anything
/// (AUTH-ABUSE-004, INT-SMS-005).
/// </summary>
/// <remarks>
/// One database serves the class, so each test counts against a number of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class SendLedgerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    /// <summary>
    /// AUTH-ABUSE-004 AC6: the destination record is a keyed hash and times and
    /// nothing else, so a dump of the table yields no address. The hash carries the
    /// version of the key it is computed under (OPS-SEC-003 AC6, entry 318 of the
    /// decisions pending review).
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync()
    {
        const string number = "+201001234561";

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'send_counters'
            ORDER BY column_name
            """);

        Assert.Equal(["fingerprint_version", "key", "sent_at"], columns);

        RestrictionKey destination = Destination(number);

        await RecordedAsync(Reference(1), [new SendCount(destination, Day)], Noon);

        SendCounterRecord stored = await FindAsync(destination)
            ?? throw new Xunit.Sdk.XunitException("The key was not counted.");

        Assert.Equal(Fingerprint.Length, stored.Key.Length);
        Assert.Equal(-1, stored.Key.AsSpan().IndexOf(Encoding.UTF8.GetBytes(number)));
        Assert.Equal([Noon], stored.SentAt);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC6: the record is gone once every bucket it counts for is
    /// empty, so a key sent to once does not stand in the table for ever.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_TheRecordIsGoneOnceItsBucketsAreEmptyAsync()
    {
        RestrictionKey destination = Destination("+201001234562");
        var source = new RestrictionKey("sms.source", "198.51.100.2");

        await RecordedAsync(Reference(2), [new SendCount(destination, TimeSpan.FromHours(1))], Noon);
        await RecordedAsync(Reference(3), [new SendCount(source, Day)], Noon + TimeSpan.FromHours(2));

        Assert.NotNull(await FindAsync(destination));

        _ = await CountedAsync(source, Noon + TimeSpan.FromHours(2));

        Assert.Null(await FindAsync(destination));
        Assert.NotNull(await FindAsync(source));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC6: what a record is kept for is what the restrictions now
    /// declare, so an interval the host shortens reaches the sends already counted.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_AShortenedIntervalReachesTheSendsAlreadyCountedAsync()
    {
        RestrictionKey destination = Destination("+201001234565");

        await RecordedAsync(Reference(10), [new SendCount(destination, Day)], Noon);

        Assert.NotNull(await FindAsync(destination));

        // The host now declares one hour where it declared a day, and the record
        // written under the day goes with the sweep of the next read.
        _ = await CountedAsync(destination, Noon + TimeSpan.FromHours(2) - TimeSpan.FromHours(1));

        Assert.Null(await FindAsync(destination));
    }

    /// <summary>
    /// PRIV-RET-005 AC2: the record lives at most the longest interval any bucket
    /// counts over, and is gone the next time the ledger is read after it.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_005_AC2_TheRecordLivesAtMostTheLongestBucketIntervalAsync()
    {
        RestrictionKey destination = Destination("+201001234567");
        var other = new RestrictionKey("sms.source", "198.51.100.7");

        TimeSpan longest = Restrictions.Retain(new Restriction(
            "sms.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Notification,
            [
                new Bucket(3, TimeSpan.FromHours(1), BucketWindow.Sliding),
                new Bucket(10, Day, BucketWindow.Sliding),
            ]));

        Assert.Equal(Day, longest);

        await RecordedAsync(Reference(8), [new SendCount(destination, longest)], Noon);

        SendCounterRecord stored = await FindAsync(destination)
            ?? throw new Xunit.Sdk.XunitException("The key was not counted.");

        Assert.Equal([Noon], stored.SentAt);

        await RecordedAsync(
            Reference(9),
            [new SendCount(other, Day)],
            Noon + longest + TimeSpan.FromSeconds(1));

        _ = await CountedAsync(other, Noon + longest + TimeSpan.FromSeconds(1) - longest);

        Assert.Null(await FindAsync(destination));
    }

    /// <summary>
    /// A send taken back out by a delivery report leaves no time behind it, and the
    /// record goes with the last one (AUTH-ABUSE-004 AC2, INT-SMS-005).
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_TheOnlySendCounted_LeavesNoRecordAsync()
    {
        RestrictionKey destination = Destination("+201001234563");
        byte[] reference = Reference(4);

        await RecordedAsync(reference, [new SendCount(destination, Day)], Noon);

        await using (StoreContext releasing = database.Context())
        {
            Assert.True(await Ledger(releasing).ReleaseAsync(reference, TestContext.Current.CancellationToken));
            await releasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Null(await FindAsync(destination));

        await using StoreContext reading = database.Context();

        Assert.False(await Ledger(reading).ReleaseAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// INT-GEN-003 AC1: a send is held under its reference until it is released, and a
    /// reference nobody drew holds nothing; asking changes nothing (INT-SMS-005).
    /// </summary>
    [Fact]
    public async Task HoldsAsync_ASendCounted_IsHeldUntilItIsReleasedAsync()
    {
        RestrictionKey destination = Destination("+201001234568");
        byte[] reference = Reference(11);

        await RecordedAsync(reference, [new SendCount(destination, Day)], Noon);

        await using (StoreContext reading = database.Context())
        {
            Assert.True(await Ledger(reading).HoldsAsync(reference, TestContext.Current.CancellationToken));
            Assert.False(await Ledger(reading).HoldsAsync(Reference(12), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(await FindAsync(destination));

        await using (StoreContext releasing = database.Context())
        {
            Assert.True(await Ledger(releasing).ReleaseAsync(reference, TestContext.Current.CancellationToken));
            await releasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext after = database.Context();

        Assert.False(await Ledger(after).HoldsAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Credit granted to a key is spent one send at a time and the row goes with the
    /// last of it (AUTH-ABUSE-004 AC4).
    /// </summary>
    [Fact]
    public async Task GrantAsync_TheCreditGranted_IsSpentOneSendAtATimeAsync()
    {
        RestrictionKey destination = Destination("+201001234564");

        await using (StoreContext granting = database.Context())
        {
            await Ledger(granting).GrantAsync(destination, 2, TestContext.Current.CancellationToken);
            await granting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RecordedAsync(Reference(5), [new SendCount(destination, Day)], Noon, [destination]);

        Assert.Equal(1, (await StandingAsync(destination, Noon - Day)).Credit);

        await RecordedAsync(Reference(6), [new SendCount(destination, Day)], Noon, [destination]);

        Assert.Equal(0, (await StandingAsync(destination, Noon - Day)).Credit);
    }

    private static RestrictionKey Destination(string number) => new("sms.destination", number);

    private static byte[] Reference(byte one) => [.. Enumerable.Repeat(one, Fingerprint.Length)];

    private static SendLedger Ledger(StoreContext context) => new(context, Deployment.FingerprintKeys);

    private async Task RecordedAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        DateTimeOffset at,
        IReadOnlyCollection<RestrictionKey>? spent = null)
    {
        await using StoreContext writing = database.Context();

        await Ledger(writing).RecordAsync(
            reference,
            counted,
            spent ?? [],
            at,
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SendCounter> StandingAsync(RestrictionKey key, DateTimeOffset stale) =>
        (await CountedAsync(key, stale))[key];

    private async Task<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountedAsync(
        RestrictionKey key,
        DateTimeOffset stale)
    {
        await using StoreContext reading = database.Context();

        return await Ledger(reading).CountersAsync([key], stale, TestContext.Current.CancellationToken);
    }

    private async Task<SendCounterRecord?> FindAsync(RestrictionKey key)
    {
        byte[] hashed = Fingerprint.Compute(
            Encoding.UTF8.GetBytes(key.Restriction + "\u0000" + key.Value),
            Deployment.FingerprintKey);

        await using StoreContext reading = database.Context();

        return await reading.SendCounters
            .SingleOrDefaultAsync(counter => counter.Key == hashed, TestContext.Current.CancellationToken);
    }
}
