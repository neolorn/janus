using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The database the library owns: its collation, its own migration history, and what it
/// does when the migrations are applied a second time
/// (OPS-DB-001, OPS-DB-002, OPS-MIG-007, CONV-ENUM-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class SchemaTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    // Two words whose Unicode order is the reverse of their byte order: the fatha on
    // the first word's opening letter outranks the second word's second letter by code
    // point, and counts for nothing until the letters themselves have been compared.
    private static readonly string[] Arabic = ["بيت", "بَحر"];

    /// <summary>
    /// OPS-DB-001 AC1: Arabic sorts by Unicode rules, which is the reverse of byte
    /// order here.
    /// </summary>
    [Fact]
    public async Task OPS_DB_001_AC1_ArabicSortsPerUnicodeRulesNotByteOrderAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> sorted = await connection.QueryAsync<string>(
            "SELECT word FROM unnest(@words) AS word ORDER BY word",
            new { words = Arabic });

        Assert.Equal(["بَحر", "بيت"], sorted);
        Assert.Equal(["بيت", "بَحر"], Arabic.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// OPS-DB-001 AC2: the case-insensitive collation the plaintext columns carry
    /// compares without regard to case.
    /// </summary>
    [Fact]
    public async Task OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        bool same = await connection.ExecuteScalarAsync<bool>(
            "SELECT 'Ahmed' = 'ahmed' COLLATE janus_ci");

        Assert.True(same);
    }

    /// <summary>
    /// OPS-DB-001 AC3: the database's own collation provider is ICU, set when the
    /// database was created and before the first migration ran.
    /// </summary>
    [Fact]
    public async Task OPS_DB_001_AC3_TheDatabaseIsCreatedUnderTheIcuLocaleAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        (char provider, string locale) = await connection.QuerySingleAsync<(char, string)>(
            "SELECT datlocprovider, datlocale FROM pg_database WHERE datname = current_database()");

        Assert.Equal('i', provider);
        Assert.Equal("und", locale);
    }

    /// <summary>
    /// OPS-DB-002 AC1: the library's migration history is a table of its own inside the
    /// schema it owns, so a host's migrations never collide with it.
    /// </summary>
    [Fact]
    public async Task OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> tables = await connection.QueryAsync<string>(
            "SELECT tablename FROM pg_tables WHERE schemaname = @schema ORDER BY tablename",
            new { schema = JanusDbContext.Schema });

        Assert.Contains(JanusDbContext.MigrationsHistoryTable, tables);
        Assert.Contains("accounts", tables);
        Assert.Contains("subject_keys", tables);
        Assert.Contains("settings", tables);
    }

    /// <summary>
    /// OPS-MIG-007 AC1: the migrations are applied a second time against the same
    /// database and change nothing, which is what the pipeline's second run asserts.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync()
    {
        await using JanusDbContext context = database.Context();
        int declared = context.Database.GetMigrations().Count();

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(declared, await AppliedAsync(connection));

        await database.MigrateAsync();

        Assert.Equal(declared, await AppliedAsync(connection));
    }

    private static async Task<int> AppliedAsync(NpgsqlConnection connection) =>
        await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM janus.\"" + JanusDbContext.MigrationsHistoryTable + "\"");

    /// <summary>
    /// CONV-ENUM-001 AC1: a value the code does not branch on is refused by the
    /// database, not only by the application.
    /// </summary>
    [Fact]
    public async Task CONV_ENUM_001_AC1_AnUnrecognisedStateIsRefusedByTheDatabaseAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "INSERT INTO janus.accounts (subject, created_at, state) VALUES (@subject, now(), 'pending')",
                new { subject = Guid.NewGuid() }));

        Assert.Equal("ck_accounts_state", refusal.ConstraintName);
    }

    /// <summary>
    /// CONV-ENUM-001 AC2: no native enum type appears in the schema; the constraint is a
    /// check, which changes without a locking migration.
    /// </summary>
    [Fact]
    public async Task CONV_ENUM_001_AC2_NoNativeEnumTypeIsInTheSchemaAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        int enums = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace "
                + "WHERE t.typtype = 'e' AND n.nspname = @schema",
            new { schema = JanusDbContext.Schema });

        Assert.Equal(0, enums);
    }

    /// <summary>
    /// PRIV-RIGHT-005a: an erased wrapped key is the only shape the column admits beside
    /// a wrapped one, so nothing of another length reaches a row.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "INSERT INTO janus.subject_keys (subject, format_marker, key_version, wrapped_key) "
                    + "VALUES (@subject, 1, 1, @key)",
                new { subject = Guid.NewGuid(), key = new byte[32] }));

        Assert.Equal("ck_subject_keys_format", refusal.ConstraintName);
    }
}
