using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Authentication.Passwords;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The password as the <c>passwords</c> table holds it: a hash, the floor flag that
/// cannot be recomputed from it, and no clock that makes it lapse (AUTH-PASS-001a,
/// AUTH-PASS-003, AUTH-PASS-007).
/// </summary>
[Trait("kind", "integration")]
public sealed class PasswordStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly Argon2StrengthClass Shipped = new(
        Janus.Core.Configuration.Settings.PasswordArgon2Memory.Default,
        Janus.Core.Configuration.Settings.PasswordArgon2Iterations.Default);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-PASS-003 AC1: nothing on the row says when the password lapses, so no
    /// sweep and no sign-in can make it lapse on a schedule.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_003_AC1_TheTableCarriesNoExpiryFieldAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'janus' AND table_name = 'passwords'
            """);

        Assert.Equal(["hash", "meets_single_factor_floor", "set_at", "subject"], columns.Order());
    }

    /// <summary>
    /// AUTH-PASS-001a AC3: the flag is written with the hash, because whether the
    /// password stands on its own cannot be read back out of the hash.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_001a_AC3_TheFloorFlagIsStoredWithTheHashAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        PasswordHash hash = Hash("a horse outstanding in its field");

        await WrittenAsync(Password.Set(subject, hash, meetsSingleFactorFloor: true, Noon));

        await using JanusDbContext reading = database.Context();
        Password read = Assert.IsType<Password>(
            await new PasswordStore(reading).FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.True(read.MeetsSingleFactorFloor);
        Assert.Equal(hash.Encoded, read.Hash.Encoded);
        Assert.Equal(Noon, read.SetAt);
    }

    /// <summary>
    /// AUTH-PASS-001a AC3: a change carries the flag the new password earns, so
    /// shortening a password below the floor is recorded and not inherited.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_001a_AC3_AChangeUpdatesTheFlagAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Password.Set(
            subject,
            Hash("a horse outstanding in its field"),
            meetsSingleFactorFloor: true,
            Noon));

        await using (JanusDbContext changing = database.Context())
        {
            Password held = Assert.IsType<Password>(await new PasswordStore(changing)
                .FindAsync(subject, TestContext.Current.CancellationToken));

            held.Change(Hash("shorter one"), meetsSingleFactorFloor: false, Noon + TimeSpan.FromDays(1));

            await new PasswordStore(changing).SetAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        Password read = Assert.IsType<Password>(
            await new PasswordStore(reading).FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.False(read.MeetsSingleFactorFloor);
        Assert.Equal(Noon + TimeSpan.FromDays(1), read.SetAt);
    }

    /// <summary>
    /// AUTH-PASS-007 AC2: the silent upgrade writes the new hash and nothing else, so
    /// the account shows the same password, set when it was set.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_007_AC2_ARehashLeavesEverythingButTheHashAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Password.Set(
            subject,
            Hash("a horse outstanding in its field"),
            meetsSingleFactorFloor: true,
            Noon));

        await using (JanusDbContext rehashing = database.Context())
        {
            Password held = Assert.IsType<Password>(await new PasswordStore(rehashing)
                .FindAsync(subject, TestContext.Current.CancellationToken));

            held.Rehash(Hash("a horse outstanding in its field", new Argon2StrengthClass(47104, 3)));

            await new PasswordStore(rehashing).RehashAsync(held, TestContext.Current.CancellationToken);
            await rehashing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        Password read = Assert.IsType<Password>(
            await new PasswordStore(reading).FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(new Argon2StrengthClass(47104, 3), read.Hash.Parameters);
        Assert.True(Argon2idHasher.Verify(
            Encoding.UTF8.GetBytes("a horse outstanding in its field"),
            read.Hash));
        Assert.True(read.MeetsSingleFactorFloor);
        Assert.Equal(Noon, read.SetAt);
    }

    /// <summary>
    /// AUTH-PASS-007: what the row carries is a hash and not the password, so a dump
    /// of the table yields nothing anyone can present.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_007_TheRowCarriesNoPasswordAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await WrittenAsync(Password.Set(
            subject,
            Hash("a horse outstanding in its field"),
            meetsSingleFactorFloor: true,
            Noon));

        await using NpgsqlConnection connection = await database.OpenAsync();

        string stored = await connection.ExecuteScalarAsync<string>(
            "SELECT hash FROM janus.passwords WHERE subject = @subject",
            new { subject = subject.Value })
            ?? throw new Xunit.Sdk.XunitException("The password was written.");

        Assert.DoesNotContain("a horse outstanding in its field", stored, StringComparison.Ordinal);
        Assert.StartsWith("$argon2id$", stored, StringComparison.Ordinal);
    }

    /// <summary>
    /// An account that has never set one holds no row, which is what the second-step
    /// rule and the sign-in path both read.
    /// </summary>
    [Fact]
    public async Task FindAsync_AnAccountThatSetNone_ReadsNothingAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using JanusDbContext reading = database.Context();

        Assert.Null(await new PasswordStore(reading)
            .FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _deployment.Dispose();
        _randomness.Dispose();
    }

    private PasswordHash Hash(string password, Argon2StrengthClass? parameters = null) =>
        new Argon2idHasher(_randomness).Hash(
            Encoding.UTF8.GetBytes(password),
            parameters ?? Shipped,
            parallelism: 1);

    private async Task WrittenAsync(Password password)
    {
        await using JanusDbContext writing = database.Context();

        await new PasswordStore(writing).SetAsync(password, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
