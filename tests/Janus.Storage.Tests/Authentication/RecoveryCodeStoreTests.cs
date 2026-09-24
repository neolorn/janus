using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Accounts;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Identity.Accounts;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// An account's recovery codes as the <c>recovery_code_sets</c> and
/// <c>recovery_codes</c> tables hold them: hashes, in the order they were drawn, one
/// set at a time (AUTH-FACT-008, AUTH-FACT-009).
/// </summary>
[Trait("kind", "integration")]
public sealed class RecoveryCodeStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    // Earlier than any set the other tests write, so the reminder query sees only
    // the sets its own test wrote.
    private static readonly DateTimeOffset LongAgo = new(2001, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Argon2StrengthClass Shipped = new(
        Janus.Core.Configuration.Settings.PasswordArgon2Memory.Default,
        Janus.Core.Configuration.Settings.PasswordArgon2Iterations.Default);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-FACT-008 AC2: the tables hold a hash per code and nothing a code could be
    /// read back out of, so the generation screen is the only place a code exists.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC2_TheTablesHoldNoColumnACodeCouldBeReadFromAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'recovery_codes'
            """);

        Assert.Equal(["hash", "ordinal", "subject", "used_at"], columns.Order());
    }

    /// <summary>
    /// AUTH-FACT-008 AC3: what stands in the rows is an Argon2id hash of each code and
    /// never the code, so a dump of both tables yields nothing presentable.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC3_ADumpOfTheTableYieldsNoUsableCodeAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IReadOnlyList<string> drawn = Drawn(3);

        await WrittenAsync(RecoveryCodeSet.Of(subject, Hashes(drawn), Noon));

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> stored = await connection.QueryAsync<string>(
            "SELECT hash FROM identity.recovery_codes WHERE subject = @subject",
            new { subject = subject.Value });

        foreach (string hash in stored)
        {
            Assert.StartsWith("$argon2id$", hash, StringComparison.Ordinal);
            Assert.DoesNotContain(
                drawn,
                code => hash.Contains(RecoveryCode.Canonical(code), StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// AUTH-FACT-009 AC1: the codes of a replaced set are gone from the table, so no
    /// code of it can be presented however the rows are read.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_009_AC1_ReplacingTheSetRemovesThePreviousCodesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IReadOnlyList<string> first = Drawn(3);
        IReadOnlyList<string> second = Drawn(2);

        await WrittenAsync(RecoveryCodeSet.Of(subject, Hashes(first), Noon));
        await WrittenAsync(RecoveryCodeSet.Of(subject, Hashes(second), Noon + TimeSpan.FromDays(1)));

        await using StoreContext reading = database.Context();
        RecoveryCodeSet read = Assert.IsType<RecoveryCodeSet>(await new RecoveryCodeStore(reading)
            .FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(2, read.Codes.Count);
        Assert.Equal(Noon + TimeSpan.FromDays(1), read.GeneratedAt);
        Assert.False(read.Spend(first[0], Noon + TimeSpan.FromDays(2)));
        Assert.True(read.Spend(second[0], Noon + TimeSpan.FromDays(2)));
    }

    /// <summary>
    /// AUTH-FACT-008 AC1: a code spent is spent on the row, so presenting it again
    /// after a restart is presenting a used code.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC1_ASpentCodeIsSpentOnTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IReadOnlyList<string> drawn = Drawn(3);

        await WrittenAsync(RecoveryCodeSet.Of(subject, Hashes(drawn), Noon));

        await using (StoreContext spending = database.Context())
        {
            RecoveryCodeSet held = Assert.IsType<RecoveryCodeSet>(await new RecoveryCodeStore(spending)
                .FindAsync(subject, TestContext.Current.CancellationToken));

            Assert.True(held.Spend(drawn[1], Noon + TimeSpan.FromHours(1)));

            await new RecoveryCodeStore(spending).RecordAsync(held, TestContext.Current.CancellationToken);
            await spending.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RecoveryCodeSet read = Assert.IsType<RecoveryCodeSet>(await new RecoveryCodeStore(reading)
            .FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(2, read.Remaining);
        Assert.Equal(Noon + TimeSpan.FromHours(1), read.Codes[1].UsedAt);
        Assert.False(read.Spend(drawn[1], Noon + TimeSpan.FromHours(2)));
    }

    /// <summary>
    /// AUTH-FACT-008 AC4: when the codes were shown, exported and reminded about is
    /// carried on the set's own row and read back with it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC4_TheTimestampsAreCarriedOnTheSetAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(RecoveryCodeSet.Of(subject, Hashes(Drawn(2)), Noon));

        await using (StoreContext marking = database.Context())
        {
            RecoveryCodeSet held = Assert.IsType<RecoveryCodeSet>(await new RecoveryCodeStore(marking)
                .FindAsync(subject, TestContext.Current.CancellationToken));

            held.Viewed(Noon + TimeSpan.FromMinutes(1));
            held.Exported(Noon + TimeSpan.FromMinutes(2));

            await new RecoveryCodeStore(marking).RecordAsync(held, TestContext.Current.CancellationToken);
            await marking.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RecoveryCodeSet read = Assert.IsType<RecoveryCodeSet>(await new RecoveryCodeStore(reading)
            .FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(Noon + TimeSpan.FromMinutes(1), read.ViewedAt);
        Assert.Equal(Noon + TimeSpan.FromMinutes(2), read.ExportedAt);
        Assert.Null(read.RemindedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: the sets owed their reminder are those generated long enough
    /// ago, never reminded of, and held by an active account, oldest first.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC5_TheSetsOwedTheirReminderAreReadOldestFirstAsync()
    {
        SubjectId oldest = await _deployment.AccountAsync(Noon);
        SubjectId older = await _deployment.AccountAsync(Noon);
        SubjectId reminded = await _deployment.AccountAsync(Noon);
        SubjectId young = await _deployment.AccountAsync(Noon);
        SubjectId suspended = await _deployment.AccountAsync(Noon);

        var remindedSet = RecoveryCodeSet.Of(reminded, [], LongAgo);
        remindedSet.Reminded(LongAgo + TimeSpan.FromDays(1));

        await WrittenAsync(RecoveryCodeSet.Of(older, [], LongAgo + TimeSpan.FromHours(1)));
        await WrittenAsync(RecoveryCodeSet.Of(oldest, [], LongAgo));
        await WrittenAsync(remindedSet);
        await WrittenAsync(RecoveryCodeSet.Of(young, [], LongAgo + TimeSpan.FromDays(2)));
        await WrittenAsync(RecoveryCodeSet.Of(suspended, [], LongAgo));

        await using (StoreContext suspending = database.Context())
        {
            var accounts = new AccountStore(suspending);
            Account account = Assert.IsType<Account>(
                await accounts.FindBySubjectAsync(suspended, TestContext.Current.CancellationToken));

            account.Suspend();
            await accounts.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
            await suspending.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        var store = new RecoveryCodeStore(reading);

        Assert.Equal(
            [oldest, older],
            await store.DueReminderAsync(
                LongAgo + TimeSpan.FromDays(1),
                count: 10,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            [oldest],
            await store.DueReminderAsync(
                LongAgo + TimeSpan.FromDays(1),
                count: 1,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An account that has generated none holds no set, which is what the account's
    /// listing and the reminder sweep both read.
    /// </summary>
    [Fact]
    public async Task FindAsync_AnAccountThatGeneratedNone_ReadsNothingAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext reading = database.Context();

        Assert.Null(await new RecoveryCodeStore(reading)
            .FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _deployment.Dispose();
        _randomness.Dispose();
    }

    private IReadOnlyList<string> Drawn(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => RecoveryCode.Draw(_randomness))];

    private IReadOnlyList<PasswordHash> Hashes(IReadOnlyList<string> drawn)
    {
        var hasher = new Argon2idHasher(_randomness);

        return
        [
            .. drawn.Select(code => hasher.Hash(
                Encoding.UTF8.GetBytes(RecoveryCode.Canonical(code)),
                Shipped,
                parallelism: 1)),
        ];
    }

    private async Task WrittenAsync(RecoveryCodeSet set)
    {
        await using StoreContext writing = database.Context();

        await new RecoveryCodeStore(writing).ReplaceAsync(set, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
