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

    private const string Notice = "privacy-notice";

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
                "newsletter-terms",
                "3",
                ConsentMechanism.Dashboard,
                ConsentKind.Written,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        ConsentRecord held = Assert.Single(
            await new ConsentStore(reading, new DataConnections(reading))
                .ConsentsAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(Recommendations, held.Purpose);
        Assert.Equal("newsletter-terms", held.Document);
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

        await using StoreContext reading = database.Context();

        ConsentRecord held = Assert.Single(
            await new ConsentStore(reading, new DataConnections(reading))
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

        await using StoreContext reading = database.Context();

        IReadOnlyList<HeldConsent> held = await new ConsentStore(reading, new DataConnections(reading))
            .LiveAgainstAnotherAsync([Recommendations], Notice, "2", TestContext.Current.CancellationToken);

        SubjectId[] mine = [asked, current, withdrawn];

        Assert.Equal([asked], held.Select(one => one.Subject).Where(mine.Contains));
    }

    /// <summary>
    /// PRIV-CONS-001, PRIV-CONS-007: a live consent is found by the document and the
    /// version it was given against, so one given against another document is found
    /// although its version is the one just published.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC1_ALiveConsentAgainstAnotherDocumentIsFoundAsync()
    {
        SubjectId elsewhere = await RegisteredAsync();
        SubjectId current = await RegisteredAsync();

        await WritingAsync(async store => await store.RecordAsync(
            elsewhere,
            Granted(Recommendations, "2"),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.RecordAsync(
            current,
            Granted(Recommendations, "2") with { Document = "newsletter-terms" },
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        IReadOnlyList<HeldConsent> held = await new ConsentStore(reading, new DataConnections(reading)).LiveAgainstAnotherAsync(
            [Recommendations],
            "newsletter-terms",
            "2",
            TestContext.Current.CancellationToken);

        SubjectId[] mine = [elsewhere, current];

        Assert.Equal([elsewhere], held.Select(one => one.Subject).Where(mine.Contains));
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
            Notice,
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

        await using StoreContext reading = database.Context();

        ObjectionRecord held = Assert.Single(
            await new ConsentStore(reading, new DataConnections(reading))
                .ObjectionsAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal("security", held.Purpose);
        Assert.Equal(Notice, held.Document);
        Assert.Equal(Noon, held.RecordedAt);
        Assert.Equal(Noon.AddDays(2), held.WithdrawnAt);
        Assert.False(held.Standing);
    }

    private static ConsentRecord Granted(string purpose, string noticeVersion) =>
        new(
            purpose,
            Notice,
            noticeVersion,
            ConsentMechanism.Dashboard,
            ConsentKind.Ordinary,
            Noon,
            WithdrawnAt: null,
            SupersededAt: null);

    private async Task<SubjectId> RegisteredAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();

        await new AccountStore(writing)
            .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    /// <summary>
    /// PRIV-CONS-008 AC5, CONV-DESIGN-003 AC6: two withdrawals of one consent at once are
    /// each decided with the subject's records held, so the second finds the consent
    /// withdrawn and one withdrawal is made.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC5_TwoWithdrawalsAtOnceWithdrawOnceAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await WritingAsync(async store => await store.RecordAsync(
            subject,
            new ConsentRecord(
                Recommendations,
                Notice,
                "1",
                ConsentMechanism.Dashboard,
                ConsentKind.Written,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            TestContext.Current.CancellationToken));

        bool[] withdrawn = await Task.WhenAll(
            WithdrawnOnceAsync(subject, Noon.AddDays(1)),
            WithdrawnOnceAsync(subject, Noon.AddDays(2)));

        Assert.Equal(1, withdrawn.Count(answer => answer));
    }

    // A withdrawal as the service makes one: the consent read before, then again with
    // the subject's records held, and withdrawn only where it still stands.
    private async Task<bool> WithdrawnOnceAsync(SubjectId subject, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var store = new ConsentStore(writing, new DataConnections(writing));

        _ = await store.ConsentsAsync(subject, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        await store.HoldAsync(subject, TestContext.Current.CancellationToken);

        ConsentRecord held = Assert.Single(await store.ConsentsAsync(subject, TestContext.Current.CancellationToken));
        bool standing = held.WithdrawnAt is null;

        if (standing)
        {
            await store.RecordAsync(subject, held with { WithdrawnAt = at }, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return standing;
    }

    private async Task WritingAsync(Func<ConsentStore, Task> write)
    {
        await using StoreContext writing = database.Context();

        await write(new ConsentStore(writing, new DataConnections(writing)));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
