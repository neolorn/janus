using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Authentication.BreakGlass;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What is kept of the break-glass credential: its issues, of which one stands at a
/// time and only a hash is held, and the attempts at it from every source
/// (OPS-BOOT-002, OPS-BOOT-004).
/// </summary>
/// <remarks>
/// One database serves the class and the tables hold the one credential of the
/// deployment, so each test begins by emptying them.
/// </remarks>
[Trait("kind", "integration")]
public sealed class BreakGlassStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// OPS-BOOT-004 AC3: a replacement leaves the new issue standing, and the previous
    /// one, already replaced, is recorded replaced no second time.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_004_AC3_AReplacementStandsInThePreviousPlaceAsync()
    {
        await EmptiedAsync();

        SubjectId administrator = await _deployment.AccountAsync(Noon);
        BreakGlassCredential first = await IssuedAsync(administrator, Noon);
        var second = BreakGlassCredential.Issue(Hash(), administrator, Noon.AddHours(1));
        bool replaced;

        await using (StoreContext writing = database.Context())
        {
            BreakGlassStore store = Store(writing);

            await using IDbContextTransaction transaction = await writing.Database
                .BeginTransactionAsync(TestContext.Current.CancellationToken);

            await store.HoldAsync(TestContext.Current.CancellationToken);

            BreakGlassCredential standing = await store.StandingAsync(TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("Nothing stood.");

            standing.Replace(Noon.AddHours(1));
            replaced = await store.RecordAsync(standing, TestContext.Current.CancellationToken);
            await store.AddAsync(second, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        first.Replace(Noon.AddHours(2));

        await using StoreContext reading = database.Context();

        Assert.True(replaced);
        Assert.Equal(second.Id, (await Store(reading).StandingAsync(TestContext.Current.CancellationToken))?.Id);
        Assert.False(await Store(reading).RecordAsync(first, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// OPS-BOOT-002 AC1: the use is recorded while the issue still stands and not a
    /// second time, and the spent issue is the one last consumed.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_002_AC1_AnIssueIsSpentOnceAsync()
    {
        await EmptiedAsync();

        SubjectId administrator = await _deployment.AccountAsync(Noon);
        BreakGlassCredential issued = await IssuedAsync(administrator, Noon);

        await using StoreContext context = database.Context();
        BreakGlassStore store = Store(context);

        BreakGlassCredential once = await store.StandingAsync(TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("Nothing stood.");
        BreakGlassCredential twice = await store.StandingAsync(TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("Nothing stood.");

        once.Consume(Noon.AddMinutes(5));
        twice.Consume(Noon.AddMinutes(6));

        Assert.True(await store.RecordAsync(once, TestContext.Current.CancellationToken));
        Assert.False(await store.RecordAsync(twice, TestContext.Current.CancellationToken));
        Assert.Null(await store.StandingAsync(TestContext.Current.CancellationToken));

        BreakGlassCredential? spent = await store.LastConsumedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(issued.Id, spent?.Id);
        Assert.Equal(Noon.AddMinutes(5), spent?.ConsumedAt);
    }

    /// <summary>
    /// OPS-BOOT-004: the database itself refuses a second issue standing beside the
    /// first.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_004_OneIssueStandsAtATimeAsync()
    {
        await EmptiedAsync();

        SubjectId administrator = await _deployment.AccountAsync(Noon);

        await IssuedAsync(administrator, Noon);

        DbUpdateException refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await IssuedAsync(administrator, Noon.AddMinutes(1)));

        Assert.Equal(
            "ux_break_glass_credentials_standing",
            Assert.IsType<PostgresException>(refusal.InnerException).ConstraintName);
    }

    /// <summary>
    /// OPS-BOOT-004: the database refuses an issue both spent and replaced.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_004_AnIssueEndsOnceAsync()
    {
        await EmptiedAsync();

        SubjectId administrator = await _deployment.AccountAsync(Noon);

        await IssuedAsync(administrator, Noon);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "UPDATE identity.break_glass_credentials SET consumed_at = @at, replaced_at = @at",
                new { at = Noon }));

        Assert.Equal("ck_break_glass_credentials_ended", refused.ConstraintName);
    }

    /// <summary>
    /// OPS-BOOT-004 AC4: what the table holds of the code is its hash, and it has no
    /// column for anything else of it.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_004_AC4_OnlyAHashOfTheCodeIsHeldAsync()
    {
        await EmptiedAsync();

        SubjectId administrator = await _deployment.AccountAsync(Noon);
        BreakGlassCredential issued = await IssuedAsync(administrator, Noon);

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'break_glass_credentials'
            ORDER BY column_name
            """);

        string? held = await connection.ExecuteScalarAsync<string>(
            "SELECT hash FROM identity.break_glass_credentials WHERE id = @id",
            new { id = issued.Id.Value });

        Assert.Equal(["consumed_at", "hash", "id", "issued_at", "issued_by", "replaced_at"], columns);
        Assert.Equal(issued.Hash.Encoded, held);
    }

    /// <summary>
    /// OPS-BOOT-004 AC7: every attempt is counted, whatever its source, within the
    /// window asked for, and the sweep forgets the ones no window reaches.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_004_AC7_EveryAttemptIsCountedWithinTheWindowAsync()
    {
        await EmptiedAsync();

        int first = await AttemptedAsync(Noon);
        int second = await AttemptedAsync(Noon.AddMinutes(40));
        int third = await AttemptedAsync(Noon.AddMinutes(80));

        await using StoreContext context = database.Context();

        int swept = await Store(context).SweepAsync(Noon.AddMinutes(20), TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 2], new[] { first, second, third });
        Assert.Equal(1, swept);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static BreakGlassStore Store(StoreContext context) => new(context, new DataConnections(context));

    private static PasswordHash Hash() =>
        PasswordHash.Of(
            new Argon2StrengthClass(19456, 2),
            1,
            RandomNumberGenerator.GetBytes(16),
            RandomNumberGenerator.GetBytes(32));

    private async Task<BreakGlassCredential> IssuedAsync(SubjectId issuedBy, DateTimeOffset at)
    {
        var issued = BreakGlassCredential.Issue(Hash(), issuedBy, at);

        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(issued, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return issued;
    }

    private async Task<int> AttemptedAsync(DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using IDbContextTransaction transaction = await writing.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);

        int counted = await Store(writing).AttemptedAsync(at, at.AddHours(-1), TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return counted;
    }

    private async Task EmptiedAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            DELETE FROM identity.break_glass_credentials;
            DELETE FROM identity.break_glass_attempts;
            """);
    }
}
