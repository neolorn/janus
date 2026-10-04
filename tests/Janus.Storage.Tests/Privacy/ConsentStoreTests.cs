using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Consents;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.Consents;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the tables hold of a consent and of an objection: a row a grant and a row an
/// objection, one live row a subject and purpose, each carrying the document and the
/// version it was decided against and the timestamps that ended it (PRIV-CONS-001,
/// PRIV-RIGHT-001a).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConsentStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Recommendations = "recommendations";

    private const string Notice = "privacy-notice";

    private const string BeforeTheKey = "20261004015227_AddLawfulBases";

    private const string TheKey = "20261004022141_KeepARecordForEachGrant";

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

        Assert.True(await WritingAsync(store => store.AddAsync(
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
            TestContext.Current.CancellationToken)));

        ConsentRecord held = Assert.Single(await ConsentsAsync(subject));

        Assert.Equal(Recommendations, held.Purpose);
        Assert.Equal("newsletter-terms", held.Document);
        Assert.Equal("3", held.NoticeVersion);
        Assert.Equal(ConsentMechanism.Dashboard, held.Mechanism);
        Assert.Equal(ConsentKind.Written, held.Kind);
        Assert.Equal(Noon, held.GrantedAt);
        Assert.True(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-001 AC1, PRIV-CONS-002 AC1: a consent given at registration is added in
    /// the unit of work that adds its account, so the account is written before the
    /// record that refers to it and both are held once it commits.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_AConsentAddedWithItsAccountInOneUnitOfWorkIsHeldAsync()
    {
        SubjectId subject = Subjects.New();

        await using (StoreContext writing = database.Context())
        await using (var work = new UnitOfWork(writing))
        {
            Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

            await new AccountStore(writing)
                .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);

            Assert.True(await new ConsentStore(writing, new DataConnections(writing)).AddAsync(
                subject,
                Granted(Recommendations, "1") with { Mechanism = ConsentMechanism.Registration },
                TestContext.Current.CancellationToken));
            Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
        }

        Assert.Equal(ConsentMechanism.Registration, Assert.Single(await ConsentsAsync(subject)).Mechanism);
    }

    /// <summary>
    /// PRIV-CONS-008 AC4: withdrawal writes a timestamp onto the record that is
    /// there, so the record survives and the evidence is not deleted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC4_WithdrawalKeepsTheRecordAndTimestampsItAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await GrantedAsync(subject, Granted(Recommendations, "1"));

        Assert.True(await WithdrawnAsync(subject, Noon.AddDays(1)));

        ConsentRecord held = Assert.Single(await ConsentsAsync(subject));

        Assert.Equal(Noon, held.GrantedAt);
        Assert.Equal(Noon.AddDays(1), held.WithdrawnAt);
        Assert.False(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-001 AC4: a grant after a withdrawal is a row of its own, and the
    /// withdrawn record stays as it was, with its document, version and instants.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_AGrantAfterAWithdrawalKeepsTheWithdrawnRecordAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await GrantedAsync(subject, Granted(Recommendations, "1"));

        Assert.True(await WithdrawnAsync(subject, Noon.AddDays(1)));

        await GrantedAsync(subject, Granted(Recommendations, "2") with { GrantedAt = Noon.AddDays(2) });

        IReadOnlyList<ConsentRecord> held = await ConsentsAsync(subject);

        Assert.Equal(2, held.Count);
        Assert.Equal("1", held[0].NoticeVersion);
        Assert.Equal(Noon, held[0].GrantedAt);
        Assert.Equal(Noon.AddDays(1), held[0].WithdrawnAt);
        Assert.Equal("2", held[1].NoticeVersion);
        Assert.Equal([false, true], held.Select(record => record.Live));
    }

    /// <summary>
    /// PRIV-CONS-001 AC4: a supersession stamps the live record and removes nothing,
    /// a grant after it is a row of its own, and a withdrawal then stamps the live
    /// record and leaves the superseded one as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_ASupersessionAndAWithdrawalEachStampTheLiveRecordAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await GrantedAsync(subject, Granted(Recommendations, "1"));

        Assert.True(await WritingAsync(store => store.SupersedeAsync(
            subject,
            Recommendations,
            Noon.AddDays(1),
            TestContext.Current.CancellationToken)));
        Assert.False(await WritingAsync(store => store.SupersedeAsync(
            subject,
            Recommendations,
            Noon.AddDays(2),
            TestContext.Current.CancellationToken)));

        await GrantedAsync(subject, Granted(Recommendations, "2") with { GrantedAt = Noon.AddDays(3) });

        Assert.True(await WithdrawnAsync(subject, Noon.AddDays(4)));

        IReadOnlyList<ConsentRecord> held = await ConsentsAsync(subject);

        Assert.Equal(2, held.Count);
        Assert.Equal(Noon.AddDays(1), held[0].SupersededAt);
        Assert.Null(held[0].WithdrawnAt);
        Assert.Null(held[1].SupersededAt);
        Assert.Equal(Noon.AddDays(4), held[1].WithdrawnAt);
    }

    /// <summary>
    /// PRIV-CONS-008: a superseded consent is the subject's to take back, so where no
    /// record is live the withdrawal stamps the latest one, once.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task WithdrawConsent_OverASupersededRecord_StampsItOnceAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await GrantedAsync(subject, Granted(Recommendations, "1"));

        Assert.True(await WritingAsync(store => store.SupersedeAsync(
            subject,
            Recommendations,
            Noon.AddDays(1),
            TestContext.Current.CancellationToken)));
        Assert.True(await WithdrawnAsync(subject, Noon.AddDays(2)));
        Assert.False(await WithdrawnAsync(subject, Noon.AddDays(3)));

        ConsentRecord held = Assert.Single(await ConsentsAsync(subject));

        Assert.Equal(Noon.AddDays(1), held.SupersededAt);
        Assert.Equal(Noon.AddDays(2), held.WithdrawnAt);
    }

    /// <summary>
    /// PRIV-CONS-001 AC4, AC6: at most one live record a subject and purpose exists. A
    /// grant while one stands is not added and fails nothing, and the database refuses
    /// a second live row whoever writes it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_AtMostOneLiveRecordASubjectAndPurposeExistsAsync()
    {
        SubjectId subject = await RegisteredAsync();

        await GrantedAsync(subject, Granted(Recommendations, "1"));

        Assert.False(await WritingAsync(store => store.AddAsync(
            subject,
            Granted(Recommendations, "2") with { GrantedAt = Noon.AddDays(1) },
            TestContext.Current.CancellationToken)));
        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            Granted("marketing", "1"),
            TestContext.Current.CancellationToken)));

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.consents
                    (id, subject, purpose, document, notice_version, mechanism, kind, granted_at)
                VALUES
                    (gen_random_uuid(), @subject, 'recommendations', 'privacy-notice', '2', 'dashboard', 'ordinary', now());
                """,
                new { subject = subject.Value }));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refused.SqlState);
        Assert.Equal("ux_consents_live", refused.ConstraintName);
        Assert.Equal("1", Assert.Single(await ConsentsAsync(subject), record => record.Purpose == Recommendations).NoticeVersion);
    }

    /// <summary>
    /// PRIV-CONS-001 AC6, CONV-DESIGN-003 AC6: two grants of one purpose at once, each
    /// in a transaction of its own, add one record; the other meets the row written
    /// meanwhile, adds none and goes on in its transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC6_TwoGrantsAtOnceAddOneRecordAsync()
    {
        SubjectId subject = await RegisteredAsync();

        bool[] added = await Task.WhenAll(
            GrantedOnceAsync(subject, "1"),
            GrantedOnceAsync(subject, "2"));

        Assert.Equal(1, added.Count(answer => answer));
        Assert.True(Assert.Single(await ConsentsAsync(subject)).Live);
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

        await GrantedAsync(asked, Granted(Recommendations, "1"));
        await GrantedAsync(current, Granted(Recommendations, "2"));
        await GrantedAsync(withdrawn, Granted(Recommendations, "1"));

        Assert.True(await WithdrawnAsync(withdrawn, Noon.AddHours(1)));

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

        await GrantedAsync(elsewhere, Granted(Recommendations, "2"));
        await GrantedAsync(current, Granted(Recommendations, "2") with { Document = "newsletter-terms" });

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

        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            Objected(Noon),
            TestContext.Current.CancellationToken)));
        Assert.True(await WritingAsync(store => store.WithdrawObjectionAsync(
            subject,
            "security",
            Noon.AddDays(2),
            TestContext.Current.CancellationToken)));

        ObjectionRecord held = Assert.Single(await ObjectionsAsync(subject));

        Assert.Equal("security", held.Purpose);
        Assert.Equal(Notice, held.Document);
        Assert.Equal(Noon, held.RecordedAt);
        Assert.Equal(Noon.AddDays(2), held.WithdrawnAt);
        Assert.False(held.Standing);
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC6: an objection while one stands is not added, and one made
    /// after a withdrawal is a row of its own beside the withdrawn one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC6_AnObjectionWhileOneStandsIsNotAddedAsync()
    {
        SubjectId subject = await RegisteredAsync();

        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            Objected(Noon),
            TestContext.Current.CancellationToken)));
        Assert.False(await WritingAsync(store => store.AddAsync(
            subject,
            Objected(Noon.AddDays(1)),
            TestContext.Current.CancellationToken)));
        Assert.True(await WritingAsync(store => store.WithdrawObjectionAsync(
            subject,
            "security",
            Noon.AddDays(2),
            TestContext.Current.CancellationToken)));
        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            Objected(Noon.AddDays(3)),
            TestContext.Current.CancellationToken)));

        IReadOnlyList<ObjectionRecord> held = await ObjectionsAsync(subject);

        Assert.Equal([Noon, Noon.AddDays(3)], held.Select(record => record.RecordedAt));
        Assert.Equal([false, true], held.Select(record => record.Standing));
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

        await GrantedAsync(subject, Granted(Recommendations, "1") with { Kind = ConsentKind.Written });

        bool[] withdrawn = await Task.WhenAll(
            WithdrawnOnceAsync(subject, Noon.AddDays(1)),
            WithdrawnOnceAsync(subject, Noon.AddDays(2)));

        Assert.Equal(1, withdrawn.Count(answer => answer));
    }

    /// <summary>
    /// PRIV-CONS-001 AC4: a record written while the tables were keyed on the subject
    /// and the purpose stays as it was under an identifier of its own, a version 7
    /// value made from the instant the record carries, and a later grant is then a row
    /// beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_ARecordWrittenBeforeTheKeyStaysUnderAnIdentifierOfItsOwnAsync()
    {
        string moved = await database.CreateDatabaseAsync("consent_key");
        SubjectId subject = Subjects.New();

        await MigrateAsync(moved, BeforeTheKey);

        await using (StoreContext writing = DatabaseFixture.Context(moved))
        {
            await new AccountStore(writing)
                .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var connection = new NpgsqlConnection(moved))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.consents
                    (subject, purpose, document, notice_version, mechanism, kind, granted_at, withdrawn_at)
                VALUES
                    (@subject, 'recommendations', 'privacy-notice', '1', 'registration', 'written', @at, @withdrawn),
                    (@subject, 'marketing', 'privacy-notice', '1', 'dashboard', 'ordinary', @at, NULL);
                INSERT INTO identity.objections
                    (subject, purpose, document, notice_version, mechanism, recorded_at)
                VALUES
                    (@subject, 'security', 'privacy-notice', '1', 'dashboard', @at);
                """,
                new { subject = subject.Value, at = Noon, withdrawn = Noon.AddDays(1) });
        }

        await MigrateAsync(moved, TheKey);

        await using var reading = new NpgsqlConnection(moved);
        await reading.OpenAsync(TestContext.Current.CancellationToken);

        string[] identifiers =
        [
            .. await reading.QueryAsync<string>(
                """
                SELECT CAST(id AS text) FROM identity.consents
                UNION ALL
                SELECT CAST(id AS text) FROM identity.objections;
                """),
        ];

        string instant = Noon.ToUnixTimeMilliseconds().ToString("x12", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(3, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(identifiers, identifier =>
        {
            Assert.Equal(7, new Guid(identifier).Version);
            Assert.Equal(instant, identifier.Replace("-", string.Empty, StringComparison.Ordinal)[..12]);
        });

        await using StoreContext context = DatabaseFixture.Context(moved);
        var store = new ConsentStore(context, new DataConnections(context));

        Assert.True(await store.AddAsync(
            subject,
            Granted(Recommendations, "2") with { GrantedAt = Noon.AddDays(2) },
            TestContext.Current.CancellationToken));

        IReadOnlyList<ConsentRecord> held =
        [
            .. (await store.ConsentsAsync(subject, TestContext.Current.CancellationToken))
                .Where(record => record.Purpose == Recommendations),
        ];

        Assert.Equal(2, held.Count);
        Assert.Equal(ConsentMechanism.Registration, held[0].Mechanism);
        Assert.Equal(ConsentKind.Written, held[0].Kind);
        Assert.Equal(Noon, held[0].GrantedAt);
        Assert.Equal(Noon.AddDays(1), held[0].WithdrawnAt);
        Assert.True(held[1].Live);
        Assert.True(Assert.Single(await store.ObjectionsAsync(subject, TestContext.Current.CancellationToken)).Standing);
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

    private static ObjectionRecord Objected(DateTimeOffset at) =>
        new("security", Notice, "1", ConsentMechanism.Dashboard, at, WithdrawnAt: null);

    private static async Task MigrateAsync(string connectionString, string target)
    {
        await using StoreContext context = DatabaseFixture.Context(connectionString);
        await context.GetService<IMigrator>().MigrateAsync(target, TestContext.Current.CancellationToken);
    }

    private async Task<SubjectId> RegisteredAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();

        await new AccountStore(writing)
            .AddAsync(Account.Create(subject, Noon), TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private async Task GrantedAsync(SubjectId subject, ConsentRecord consent) =>
        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            consent,
            TestContext.Current.CancellationToken)));

    private async Task<bool> WithdrawnAsync(SubjectId subject, DateTimeOffset at) =>
        await WritingAsync(store => store.WithdrawConsentAsync(
            subject,
            Recommendations,
            at,
            TestContext.Current.CancellationToken));

    private async Task<IReadOnlyList<ConsentRecord>> ConsentsAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new ConsentStore(reading, new DataConnections(reading))
            .ConsentsAsync(subject, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new ConsentStore(reading, new DataConnections(reading))
            .ObjectionsAsync(subject, TestContext.Current.CancellationToken);
    }

    // A grant as two requests make one at the same moment: each in a transaction of its
    // own, neither holding the subject's records, so the index alone decides.
    private async Task<bool> GrantedOnceAsync(SubjectId subject, string version)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var store = new ConsentStore(writing, new DataConnections(writing));

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        bool added = await store.AddAsync(
            subject,
            Granted(Recommendations, version),
            TestContext.Current.CancellationToken);

        _ = await store.ConsentsAsync(subject, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return added;
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
        bool standing = held.WithdrawnAt is null
            && await store.WithdrawConsentAsync(
                subject,
                Recommendations,
                at,
                TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return standing;
    }

    private async Task<bool> WritingAsync(Func<ConsentStore, ValueTask<bool>> write)
    {
        await using StoreContext writing = database.Context();

        return await write(new ConsentStore(writing, new DataConnections(writing)));
    }
}
