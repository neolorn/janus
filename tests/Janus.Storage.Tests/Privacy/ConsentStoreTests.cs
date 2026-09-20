using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Consents;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.Consents;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the tables hold of a consent and of an objection: one row a subject and
/// purpose, carrying the version of the notice it was decided against and the
/// timestamps that ended it (PRIV-CONS-001, PRIV-RIGHT-001a).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConsentStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Recommendations = "recommendations";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// PRIV-CONS-001 AC1: a consent reads back carrying the purpose, the notice
    /// version, the mechanism and the timestamp it was written with.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_AConsentReadsBackEveryFieldItWasWrittenWithAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await WritingAsync(async store => await store.RecordAsync(
            subject,
            new ConsentRecord(
                Recommendations,
                "3",
                ConsentMechanism.Dashboard,
                ConsentKind.Written,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        ConsentRecord held = Assert.Single(
            await new ConsentStore(reading)
                .ConsentsAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(Recommendations, held.Purpose);
        Assert.Equal("3", held.NoticeVersion);
        Assert.Equal(ConsentMechanism.Dashboard, held.Mechanism);
        Assert.Equal(ConsentKind.Written, held.Kind);
        Assert.Equal(Noon, held.GrantedAt);
        Assert.True(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-008 AC4: withdrawal writes a timestamp onto the record that is
    /// there, so one row a purpose survives and the evidence is not deleted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC4_WithdrawalKeepsTheRecordAndTimestampsItAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await WritingAsync(async store => await store.RecordAsync(
            subject,
            Granted(Recommendations, "1"),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.RecordAsync(
            subject,
            Granted(Recommendations, "1") with { WithdrawnAt = Noon.AddDays(1) },
            TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        ConsentRecord held = Assert.Single(
            await new ConsentStore(reading)
                .ConsentsAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(Noon.AddDays(1), held.WithdrawnAt);
        Assert.False(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-007 AC1: the subjects a material revision must ask again are those
    /// holding a live consent against an earlier version, and nobody else.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync()
    {
        SubjectId asked = await RegisteredAsync();
        SubjectId current = await RegisteredAsync();
        SubjectId withdrawn = await RegisteredAsync();

        await WritingAsync(async store => await store.RecordAsync(
            asked,
            Granted(Recommendations, "1"),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.RecordAsync(
            current,
            Granted(Recommendations, "2"),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.RecordAsync(
            withdrawn,
            Granted(Recommendations, "1") with { WithdrawnAt = Noon.AddHours(1) },
            TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        IReadOnlyList<HeldConsent> held = await new ConsentStore(reading)
            .LiveAgainstAnotherAsync("2", TestContext.Current.CancellationToken);

        Assert.Equal([asked], held.Select(one => one.Subject));
    }

    /// <summary>
    /// PRIV-RIGHT-001a: an objection reads back as it was written, and withdrawing it
    /// leaves the record standing with the timestamp that ended it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC1_AnObjectionReadsBackAndItsWithdrawalIsTimestampedAsync()
    {
        SubjectId subject = await RegisteredAsync();
        var objection = new ObjectionRecord(
            "security",
            "1",
            ConsentMechanism.Dashboard,
            Noon,
            WithdrawnAt: null);

        await WritingAsync(async store => await store.RecordAsync(
            subject,
            objection,
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.RecordAsync(
            subject,
            objection with { WithdrawnAt = Noon.AddDays(2) },
            TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        ObjectionRecord held = Assert.Single(
            await new ConsentStore(reading)
                .ObjectionsAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal("security", held.Purpose);
        Assert.Equal(Noon, held.RecordedAt);
        Assert.Equal(Noon.AddDays(2), held.WithdrawnAt);
        Assert.False(held.Standing);
    }

    private static ConsentRecord Granted(string purpose, string noticeVersion) =>
        new(
            purpose,
            noticeVersion,
            ConsentMechanism.Dashboard,
            ConsentKind.Ordinary,
            Noon,
            WithdrawnAt: null,
            SupersededAt: null);

    private async Task<SubjectId> RegisteredAsync()
    {
        SubjectId subject = Subjects.New();

        await using JanusDbContext writing = database.Context();

        await new AccountStore(writing)
            .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private async Task WritingAsync(Func<ConsentStore, Task> write)
    {
        await using JanusDbContext writing = database.Context();

        await write(new ConsentStore(writing));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
