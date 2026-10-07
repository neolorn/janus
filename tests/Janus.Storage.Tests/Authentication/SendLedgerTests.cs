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

    // What a send counts once where no restriction marked resend applies to it.
    private static readonly SendPlan Nothing = new([], []);

    private static readonly TimeSpan Pace = TimeSpan.FromMinutes(3);

    /// <summary>
    /// AUTH-ABUSE-004 AC6: the destination record, and the record of every other key,
    /// is a keyed hash and times and nothing else, so a dump of either table yields no
    /// address. The hash carries the version of the key it is computed under
    /// (OPS-SEC-003 AC6, entry 318 of the decisions pending review).
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync()
    {
        const string number = "+201001234561";

        await using NpgsqlConnection connection = await database.OpenAsync();

        foreach (string table in (string[])["send_counters", "send_key_counters"])
        {
            IEnumerable<string> columns = await connection.QueryAsync<string>(
                """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'identity' AND table_name = @table
                ORDER BY column_name
                """,
                new { table });

            Assert.Equal(["fingerprint_version", "key", "sent_at"], columns);
        }

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
        var source = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.2");

        await RecordedAsync(Reference(2), [new SendCount(destination, TimeSpan.FromHours(1))], Noon);
        await RecordedAsync(Reference(3), [new SendCount(source, Day)], Noon + TimeSpan.FromHours(2));

        Assert.NotNull(await FindAsync(destination));

        _ = await CountedAsync(source, Noon + TimeSpan.FromHours(2));

        Assert.Null(await FindAsync(destination));
        Assert.NotNull(await FindKeyAsync(source));
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
        _ = await CountedAsync(
            new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.5"),
            Noon + TimeSpan.FromHours(2) - TimeSpan.FromHours(1));

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
        var other = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7");

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
    /// PRIV-RET-005 AC2: a destination is counted apart from every other key, so a
    /// source restriction counting over a day keeps no destination record past the
    /// hour its own restriction counts over.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_005_AC2_ALongerSourceRestrictionKeepsNoDestinationRecordAsync()
    {
        RestrictionKey destination = Destination("+201001234569");
        var source = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.9");

        Restriction[] declared =
        [
            new(
                "sms.destination",
                RestrictionKeyKind.Destination,
                HostKeyName: null,
                RestrictionPurpose.Any,
                [new Bucket(3, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
            new(
                "sms.source",
                RestrictionKeyKind.Source,
                HostKeyName: null,
                RestrictionPurpose.Any,
                [new Bucket(10, Day, BucketWindow.Sliding)]),
        ];

        await RecordedAsync(
            Reference(13),
            [new SendCount(destination, TimeSpan.FromHours(1)), new SendCount(source, Day)],
            Noon);

        Assert.NotNull(await FindAsync(destination));
        Assert.NotNull(await FindKeyAsync(source));

        _ = await HeldOnceAsync([source], CounterStaleness.Of(declared, Noon + TimeSpan.FromHours(2)), setAside: null);

        Assert.Null(await FindAsync(destination));
        Assert.NotNull(await FindKeyAsync(source));
        Assert.Null(await FindAsync(source));
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

    /// <summary>
    /// AUTH-ABUSE-004 AC1, CONV-DESIGN-003 AC6: sends counted against one key at once
    /// each change the record as it then stands, so every one of them is counted.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_SendsCountedAtOnceAreEachCountedAsync()
    {
        RestrictionKey destination = Destination("+201001234570");

        await Task.WhenAll(Enumerable.Range(0, 5).Select(at =>
            CountedOnceAsync(Reference((byte)(40 + at)), [new SendCount(destination, Day)], Noon.AddMinutes(at), [])));

        Assert.Equal(5, (await StandingAsync(destination, Noon - Day)).Sends.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4, CONV-DESIGN-003 AC6: sends spending one key's credit at once
    /// each spend it as it then stands, so two credits are spent by two sends and a
    /// third finds none.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_SendsSpendingCreditAtOnceSpendEachCreditOnceAsync()
    {
        RestrictionKey destination = Destination("+201001234571");

        await using (StoreContext granting = database.Context())
        {
            await Ledger(granting).GrantAsync(destination, 2, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(Enumerable.Range(0, 3).Select(at =>
            CountedOnceAsync(Reference((byte)(50 + at)), [new SendCount(destination, Day)], Noon, [destination])));

        Assert.Equal(0, (await StandingAsync(destination, Noon - Day)).Credit);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4, CONV-DESIGN-003 AC6: credit granted to one key twice at once
    /// is added to the row as it stands, so both grants stand.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_CreditGrantedAtOnceIsAddedTwiceAsync()
    {
        RestrictionKey destination = Destination("+201001234572");

        await Task.WhenAll(GrantedOnceAsync(destination, 2), GrantedOnceAsync(destination, 3));

        Assert.Equal(5, (await StandingAsync(destination, Noon - Day)).Credit);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC2, INT-SMS-005, CONV-DESIGN-003 AC6: two reports releasing one send
    /// at once each read it under its lock, so it is released once and a time counted
    /// at the same instant for another send stays.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC2_ASendReleasedTwiceAtOnceIsReleasedOnceAsync()
    {
        RestrictionKey destination = Destination("+201001234573");
        byte[] reference = Reference(60);

        await RecordedAsync(reference, [new SendCount(destination, Day)], Noon);
        await RecordedAsync(Reference(61), [new SendCount(destination, Day)], Noon);

        bool[] released = await Task.WhenAll(ReleasedOnceAsync(reference), ReleasedOnceAsync(reference));

        Assert.Equal(1, released.Count(answer => answer));
        Assert.Equal([Noon], (await StandingAsync(destination, Noon - Day)).Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16, CONV-DESIGN-003 AC6: each judgement holds the counter of its
    /// key to the end of its transaction, creating it where none stands, so of several
    /// sends judged at once while the bucket has room for one, one finds the room and
    /// the others find it taken.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_SendsJudgedAtOnceAreJudgedOneAfterTheOtherAsync()
    {
        RestrictionKey destination = Destination("+201001234574");

        bool[] admitted = await Task.WhenAll(Enumerable.Range(0, 6).Select(one =>
            AdmittedWhereRoomAsync(destination, Reference((byte)(70 + one)), Noon.AddSeconds(one))));

        Assert.Equal(1, admitted.Count(answer => answer));
        Assert.Single((await StandingAsync(destination, Noon - Day)).Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16, AC9: a send judged again is judged with its own count set
    /// aside under the same hold, so the counter it reads holds none of it, the credit
    /// it spent stands again, and nothing answers to its reference until it counts anew.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_ARetriedSendIsJudgedWithItsOwnCountSetAsideAsync()
    {
        RestrictionKey destination = Destination("+201001234575");
        byte[] reference = Reference(80);

        await GrantedOnceAsync(destination, 1);
        await CountedOnceAsync(Reference(81), [new SendCount(destination, Day)], Noon, []);
        await CountedOnceAsync(reference, [new SendCount(destination, Day)], Noon.AddMinutes(1), [destination]);

        Assert.Equal(0, (await StandingAsync(destination, Noon - Day)).Credit);

        IReadOnlyDictionary<RestrictionKey, SendCounter> held = await HeldOnceAsync(
            [destination],
            new CounterStaleness(Noon - Day, Noon - Day),
            reference);

        Assert.Equal([Noon], held[destination].Sends);
        Assert.Equal(1, held[destination].Credit);

        await using StoreContext reading = database.Context();

        Assert.False(await Ledger(reading).HoldsAsync(reference, TestContext.Current.CancellationToken));
        Assert.True(await Ledger(reading).HoldsAsync(Reference(81), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16, INT-SMS-005 AC3: a send that fails for good is taken back out
    /// of its bucket, and the credit it spent is spent no longer.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_AReleasedSendGivesBackTheCreditItSpentAsync()
    {
        RestrictionKey destination = Destination("+201001234576");
        byte[] reference = Reference(82);

        await GrantedOnceAsync(destination, 1);
        await CountedOnceAsync(reference, [new SendCount(destination, Day)], Noon, [destination]);

        Assert.Equal(0, (await StandingAsync(destination, Noon - Day)).Credit);

        Assert.True(await ReleasedOnceAsync(reference));

        SendCounter standing = await StandingAsync(destination, Noon - Day);

        Assert.Equal(1, standing.Credit);
        Assert.Empty(standing.Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC6, PRIV-RET-005 AC2: a counter created for a judgement that
    /// counted nothing holds no time, and goes with the next sweep whatever the
    /// instants, so a refused send leaves no record standing.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_ACounterThatCountedNothingGoesWithTheNextSweepAsync()
    {
        RestrictionKey destination = Destination("+201001234577");
        var stale = new CounterStaleness(Noon - Day, Noon - Day);

        Assert.Empty((await HeldOnceAsync([destination], stale, setAside: null))[destination].Sends);

        SendCounterRecord created = await FindAsync(destination)
            ?? throw new Xunit.Sdk.XunitException("No counter was created for the judgement.");

        Assert.Empty(created.SentAt);

        await using (StoreContext sweeping = database.Context())
        {
            await Ledger(sweeping).SweepAsync(stale, TestContext.Current.CancellationToken);
        }

        Assert.Null(await FindAsync(destination));
    }
    /// <summary>
    /// AUTH-ABUSE-004 AC1 and chapter 10 section 5.16a: a send of two messages counts
    /// each against the key of a restriction not marked <c>resend</c> and once against
    /// the key of one that is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASendOfTwoMessagesCountsOnceAgainstAResendKeyAsync()
    {
        RestrictionKey each = Destination("+201001234580");
        RestrictionKey once = Resend("+201001234580");

        await SentAsync([Reference(150), Reference(151)], each, once, Noon, secondFactor: false);

        Assert.Equal([Noon, Noon], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Equal([Noon], (await StandingAsync(once, Noon - Day)).Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC2, AC16, INT-SMS-005 AC3: a message that fails for good is taken
    /// back out of the bucket of a restriction not marked <c>resend</c>, and what its
    /// send counted once stands until the last message of the send has failed for good.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_TheResendCountIsReleasedWhenTheLastMessageOfTheSendFailsAsync()
    {
        RestrictionKey each = Destination("+201001234581");
        RestrictionKey once = Resend("+201001234581");

        await SentAsync([Reference(152), Reference(153)], each, once, Noon, secondFactor: false);

        Assert.True(await ReleasedOnceAsync(Reference(152)));
        Assert.Equal([Noon], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Equal([Noon], (await StandingAsync(once, Noon - Day)).Sends);

        Assert.True(await ReleasedOnceAsync(Reference(153)));
        Assert.Empty((await StandingAsync(each, Noon - Day)).Sends);
        Assert.Empty((await StandingAsync(once, Noon - Day)).Sends);
        Assert.False(await ReleasedOnceAsync(Reference(153)));
    }

    /// <summary>
    /// INT-SMS-005 AC3, AUTH-ABUSE-004 AC20: a report that a message was delivered
    /// releases what a send of purpose <c>secondfactor</c> counted once and nothing
    /// else, changes nothing of a send of any other purpose, and answers to no
    /// reference that no counted message holds.
    /// </summary>
    /// <param name="secondFactor">Whether the send's purpose is <c>secondfactor</c>.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task INT_SMS_005_AC3_ADeliveredReportReleasesTheResendCountOfASecondFactorSendAloneAsync(bool secondFactor)
    {
        string number = secondFactor ? "+201001234582" : "+201001234583";
        byte[] reference = Reference(secondFactor ? (byte)154 : (byte)155);
        RestrictionKey each = Destination(number);
        RestrictionKey once = Resend(number);

        await SentAsync([reference], each, once, Noon, secondFactor);

        Assert.True(await InWorkAsync(ledger => ledger.DeliveredAsync(reference, TestContext.Current.CancellationToken)));
        Assert.False(await InWorkAsync(ledger => ledger.DeliveredAsync(Reference(156), TestContext.Current.CancellationToken)));

        DateTimeOffset[] standing = secondFactor ? [] : [Noon];

        Assert.Equal([Noon], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Equal(standing, (await StandingAsync(once, Noon - Day)).Sends);

        await using StoreContext reading = database.Context();

        Assert.True(await Ledger(reading).HoldsAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: what names the code or the link a send carries releases, once
    /// it is answered, what the send counted once, and nothing else; an answer no send
    /// carries changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_AnAnswerReleasesTheResendCountOfTheSendItNamesAsync()
    {
        RestrictionKey each = Destination("+201001234584");
        RestrictionKey once = Resend("+201001234584");
        byte[] answer = Reference(157);

        await SentAsync([Reference(158), Reference(159)], each, once, Noon, secondFactor: false);

        _ = await InWorkAsync(async ledger =>
        {
            await ledger.BindAsync(Reference(158), answer, TestContext.Current.CancellationToken);

            return true;
        });

        await AnsweredOnceAsync(Reference(160));

        Assert.Equal([Noon], (await StandingAsync(once, Noon - Day)).Sends);

        await AnsweredOnceAsync(answer);

        Assert.Equal([Noon, Noon], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Empty((await StandingAsync(once, Noon - Day)).Sends);

        await using StoreContext reading = database.Context();

        Assert.True(await Ledger(reading).HoldsAsync(Reference(158), TestContext.Current.CancellationToken));
        Assert.True(await Ledger(reading).HoldsAsync(Reference(159), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16, AC9: a message judged again is judged with its own count
    /// and its send's one count set aside, counts at the instant of the retry with the
    /// send in place of the earlier counts, and stays of its send: the send's one count
    /// is released only when the last of its messages has failed for good.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_AMessageJudgedAgainLeavesItsSendsOneCountOutOfTheJudgementAsync()
    {
        RestrictionKey each = Destination("+201001234585");
        RestrictionKey once = Resend("+201001234585");
        DateTimeOffset retried = Noon.AddMinutes(1);

        await SentAsync([Reference(161), Reference(162)], each, once, Noon, secondFactor: false);

        IReadOnlyDictionary<RestrictionKey, SendCounter> held = await InWorkAsync(async ledger =>
        {
            IReadOnlyDictionary<RestrictionKey, SendCounter> judged = await ledger.HoldAsync(
                [each, once],
                new CounterStaleness(Noon - Day, Noon - Day),
                Reference(162),
                TestContext.Current.CancellationToken);

            await ledger.RecordAsync(
                [new CountedMessage(Reference(162), new SendPlan([new SendCount(each, Day)], []))],
                new SendPlan([new SendCount(once, Pace)], []),
                secondFactor: false,
                retried,
                TestContext.Current.CancellationToken);

            return judged;
        });

        Assert.Equal([Noon], held[each].Sends);
        Assert.Empty(held[once].Sends);
        Assert.Equal([Noon, retried], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Equal([retried], (await StandingAsync(once, Noon - Day)).Sends);

        Assert.True(await ReleasedOnceAsync(Reference(161)));
        Assert.Equal([retried], (await StandingAsync(once, Noon - Day)).Sends);

        Assert.True(await ReleasedOnceAsync(Reference(162)));
        Assert.Empty((await StandingAsync(once, Noon - Day)).Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC9, AC16: a message set aside to be judged again holds the count
    /// of no admission and has not failed for good, so it stays of its send and the
    /// send's one count stands until it too fails for good.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC9_AMessageSetAsideStaysOfItsSendAsync()
    {
        RestrictionKey each = Destination("+201001234586");
        RestrictionKey once = Resend("+201001234586");

        await SentAsync([Reference(163), Reference(164)], each, once, Noon, secondFactor: false);

        _ = await InWorkAsync(async ledger =>
        {
            await ledger.SetAsideAsync(Reference(164), TestContext.Current.CancellationToken);

            return true;
        });

        await using (StoreContext reading = database.Context())
        {
            Assert.False(await Ledger(reading).HoldsAsync(Reference(164), TestContext.Current.CancellationToken));
            Assert.True(await Ledger(reading).HoldsAsync(Reference(163), TestContext.Current.CancellationToken));
        }

        Assert.Equal([Noon], (await StandingAsync(each, Noon - Day)).Sends);
        Assert.Equal([Noon], (await StandingAsync(once, Noon - Day)).Sends);

        Assert.True(await ReleasedOnceAsync(Reference(163)));
        Assert.Equal([Noon], (await StandingAsync(once, Noon - Day)).Sends);

        Assert.True(await ReleasedOnceAsync(Reference(164)));
        Assert.Empty((await StandingAsync(each, Noon - Day)).Sends);
        Assert.Empty((await StandingAsync(once, Noon - Day)).Sends);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16: a send is forgotten whole. A message whose own settling time
    /// has passed stands while another of its send could still be reported on, so the
    /// last of them to fail is still known as the last.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_AMessageIsForgottenOnlyWithItsSendAsync()
    {
        RestrictionKey each = new("sms.hourly", RestrictionKeyKind.Destination, "+201001234587");
        RestrictionKey once = Resend("+201001234587");
        var hour = TimeSpan.FromHours(1);
        DateTimeOffset retried = Noon.AddMinutes(30);

        _ = await InWorkAsync(async ledger =>
        {
            await ledger.RecordAsync(
                [
                    new CountedMessage(Reference(165), new SendPlan([new SendCount(each, hour)], [])),
                    new CountedMessage(Reference(166), new SendPlan([new SendCount(each, hour)], [])),
                ],
                new SendPlan([new SendCount(once, Pace)], []),
                secondFactor: false,
                Noon,
                TestContext.Current.CancellationToken);

            return true;
        });

        _ = await InWorkAsync(async ledger =>
        {
            _ = await ledger.HoldAsync(
                [each, once],
                new CounterStaleness(Noon - Day, Noon - Day),
                Reference(166),
                TestContext.Current.CancellationToken);

            await ledger.RecordAsync(
                [new CountedMessage(Reference(166), new SendPlan([new SendCount(each, hour)], []))],
                new SendPlan([new SendCount(once, Pace)], []),
                secondFactor: false,
                retried,
                TestContext.Current.CancellationToken);

            return true;
        });

        Assert.Equal(2, await SweptAsync(Reference(165), Noon.AddMinutes(70)));
        Assert.Equal(0, await SweptAsync(Reference(165), Noon.AddMinutes(100)));
    }


    private static RestrictionKey Destination(string number) => new("sms.destination", RestrictionKeyKind.Destination, number);

    private static RestrictionKey Resend(string number) => new("sms.resend", RestrictionKeyKind.Destination, number);

    private static byte[] Reference(byte one) => [.. Enumerable.Repeat(one, Fingerprint.Length)];

    private static SendLedger Ledger(StoreContext context) => new(context, new DataConnections(context), Deployment.Fingerprints);

    private async Task RecordedAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        DateTimeOffset at,
        IReadOnlyCollection<RestrictionKey>? spent = null)
    {
        await using StoreContext writing = database.Context();

        await Ledger(writing).RecordAsync(
            [new CountedMessage(reference, new SendPlan([.. counted], [.. spent ?? []]))],
            Nothing,
            secondFactor: false,
            at,
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A send counted as the sending service counts one, in a transaction of its own.
    private async Task CountedOnceAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        DateTimeOffset at,
        IReadOnlyCollection<RestrictionKey> spent)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await Ledger(writing).RecordAsync(
            [new CountedMessage(reference, new SendPlan([.. counted], [.. spent]))],
            Nothing,
            secondFactor: false,
            at,
            TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }

    // One send judged as an admission judges it against a bucket of one: the counter
    // held, the send counted where the bucket has room, and the transaction ended.
    private async Task<bool> AdmittedWhereRoomAsync(RestrictionKey key, byte[] reference, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        SendLedger ledger = Ledger(writing);

        IReadOnlyDictionary<RestrictionKey, SendCounter> held = await ledger.HoldAsync(
            [key],
            new CounterStaleness(Noon - Day, Noon - Day),
            setAside: null,
            TestContext.Current.CancellationToken);

        if (held[key].Sends.Count > 0)
        {
            await work.RollbackAsync();

            return false;
        }

        await ledger.RecordAsync(
            [new CountedMessage(reference, new SendPlan([new SendCount(key, Day)], []))],
            Nothing,
            secondFactor: false,
            at,
            TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return true;
    }

    private async Task GrantedOnceAsync(RestrictionKey key, int credit)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await Ledger(writing).GrantAsync(key, credit, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }

    // A delivery report's release, in a transaction of its own.
    private async Task<bool> ReleasedOnceAsync(byte[] reference)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        bool released = await Ledger(writing).ReleaseAsync(reference, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return released;
    }

    // One send admitted as the sending service admits one: the counters held, each
    // message counted against the one key and the send once against the other, in a
    // transaction of its own.
    private Task<bool> SentAsync(
        byte[][] references,
        RestrictionKey each,
        RestrictionKey once,
        DateTimeOffset at,
        bool secondFactor) =>
        InWorkAsync(async ledger =>
        {
            _ = await ledger.HoldAsync(
                [each, once],
                new CounterStaleness(Noon - Day, Noon - Day),
                setAside: null,
                TestContext.Current.CancellationToken);

            await ledger.RecordAsync(
                [.. references.Select(reference =>
                    new CountedMessage(reference, new SendPlan([new SendCount(each, Day)], [])))],
                new SendPlan([new SendCount(once, Pace)], []),
                secondFactor,
                at,
                TestContext.Current.CancellationToken);

            return true;
        });

    // A code presented right or a link pressed, in a transaction of its own.
    private Task<bool> AnsweredOnceAsync(byte[] answer) =>
        InWorkAsync(async ledger =>
        {
            await ledger.AnsweredAsync(answer, TestContext.Current.CancellationToken);

            return true;
        });

    // The settled forgotten as of an instant, and how many messages of one send stand.
    private async Task<int> SweptAsync(byte[] send, DateTimeOffset now)
    {
        _ = await InWorkAsync(async ledger =>
        {
            await ledger.SweepSettledAsync(now, TestContext.Current.CancellationToken);

            return true;
        });

        await using StoreContext reading = database.Context();

        return await reading.Sends.CountAsync(message => message.Send == send, TestContext.Current.CancellationToken);
    }

    // One operation of the ledger in a transaction of its own that commits.
    private async Task<TValue> InWorkAsync<TValue>(Func<SendLedger, ValueTask<TValue>> operation)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        TValue value = await operation(Ledger(writing));

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return value;
    }

    private async Task<SendCounter> StandingAsync(RestrictionKey key, DateTimeOffset stale) =>
        (await CountedAsync(key, stale))[key];

    private Task<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountedAsync(
        RestrictionKey key,
        DateTimeOffset stale) =>
        HeldOnceAsync([key], new CounterStaleness(stale, stale), setAside: null);

    // The counters as a judgement takes them, held in a transaction of its own that
    // commits, as an admitted send's does.
    private async Task<IReadOnlyDictionary<RestrictionKey, SendCounter>> HeldOnceAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        CounterStaleness stale,
        byte[]? setAside)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        IReadOnlyDictionary<RestrictionKey, SendCounter> held = await Ledger(writing)
            .HoldAsync(keys, stale, setAside, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return held;
    }

    private static byte[] Hashed(RestrictionKey key) =>
        Fingerprint.Compute(
            Encoding.UTF8.GetBytes(key.Restriction + "\u0000" + key.Value),
            Deployment.FingerprintKey);

    // A destination's record, which is kept apart from every other key's.
    private async Task<SendCounterRecord?> FindAsync(RestrictionKey key)
    {
        byte[] hashed = Hashed(key);

        await using StoreContext reading = database.Context();

        return await reading.SendCounters
            .SingleOrDefaultAsync(counter => counter.Key == hashed, TestContext.Current.CancellationToken);
    }

    // The record of a key other than a destination.
    private async Task<SendKeyCounterRecord?> FindKeyAsync(RestrictionKey key)
    {
        byte[] hashed = Hashed(key);

        await using StoreContext reading = database.Context();

        return await reading.SendKeyCounters
            .SingleOrDefaultAsync(counter => counter.Key == hashed, TestContext.Current.CancellationToken);
    }
}
