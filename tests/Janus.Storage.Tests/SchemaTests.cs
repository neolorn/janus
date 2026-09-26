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
/// (OPS-DB-001, OPS-DB-002, INF-DB-001, OPS-MIG-007, CONV-ENUM-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class SchemaTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string ListDomain =
        "INSERT INTO identity.organization_domains (token, organization, domain, added_at) "
            + "VALUES (@token, @organization, @domain, now())";

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
            "SELECT 'Ahmed' = 'ahmed' COLLATE identity.identity_ci");

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
            new { schema = StoreContext.Schema });

        Assert.Contains(StoreContext.MigrationsHistoryTable, tables);
        Assert.Contains("accounts", tables);
        Assert.Contains("subject_keys", tables);
        Assert.Contains("settings", tables);
    }

    /// <summary>
    /// OPS-DB-002 AC1: the collation is the library's, so it is created in the library's
    /// own schema and nowhere else. A column names it by that schema.
    /// </summary>
    [Fact]
    public async Task OPS_DB_002_AC1_TheCollationLivesInTheLibrarysSchemaAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> schemas = await connection.QueryAsync<string>(
            """
            SELECT held.nspname
            FROM pg_collation AS defined
            JOIN pg_namespace AS held ON held.oid = defined.collnamespace
            WHERE defined.collname = @name
            """,
            new { name = StoreContext.CaseInsensitiveCollation });

        Assert.Equal([StoreContext.Schema], schemas);
    }

    /// <summary>
    /// INF-DB-001 AC3: database-level case-insensitive comparison applies to the
    /// plaintext columns, read from the catalogue. The columns that carry the collation
    /// are exactly the ones OPS-DB-001 names, the plaintext a person spells and the
    /// library compares: organization names and locked domain names. No other column
    /// carries it, since identifiers are fingerprints whose case-insensitivity comes from
    /// canonicalisation before the keyed function.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_DB_001_AC3_ThePlaintextColumnsComparedByValueCarryTheCollationAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> collated = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT held.relname || '.' || attribute.attname
            FROM pg_attribute AS attribute
            JOIN pg_class AS held ON held.oid = attribute.attrelid
            JOIN pg_namespace AS space ON space.oid = held.relnamespace
            JOIN pg_collation AS defined ON defined.oid = attribute.attcollation
            JOIN pg_namespace AS definer ON definer.oid = defined.collnamespace
            WHERE space.nspname = @schema
              AND held.relkind = 'r'
              AND attribute.attnum > 0
              AND NOT attribute.attisdropped
              AND defined.collname = @name
              AND definer.nspname = @schema
            ORDER BY 1
            """,
            new { schema = StoreContext.Schema, name = StoreContext.CaseInsensitiveCollation },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(["organization_domains.domain", "organizations.name"], collated);
    }

    /// <summary>
    /// INF-DB-001 AC3: the comparison is the database's own, so a locked domain listed a
    /// second time for one organization in other capitals is refused by the unique index
    /// even where nothing before it folded the case.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_DB_001_AC3_ALockedDomainInOtherCapitalsIsRefusedByTheDatabaseAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        var organization = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO identity.organizations (id, name, canonical_name, created_at) "
                + "VALUES (@organization, 'Acme', 'acme', now())",
            new { organization },
            cancellationToken: TestContext.Current.CancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            ListDomain,
            new { token = Guid.NewGuid().ToString("N"), organization, domain = "example.com" },
            cancellationToken: TestContext.Current.CancellationToken));

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(new CommandDefinition(
                ListDomain,
                new { token = Guid.NewGuid().ToString("N"), organization, domain = "Example.COM" },
                cancellationToken: TestContext.Current.CancellationToken)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refusal.SqlState);
        Assert.Equal("ux_organization_domains_organization_domain", refusal.ConstraintName);
    }

    /// <summary>
    /// OPS-MIG-007 AC1: the migrations are applied a second time against the same
    /// database and change nothing, which is what the pipeline's second run asserts.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync()
    {
        await using StoreContext context = database.Context();
        int declared = context.Database.GetMigrations().Count();

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(declared, await AppliedAsync(connection));

        await database.MigrateAsync();

        Assert.Equal(declared, await AppliedAsync(connection));
    }

    private static async Task<int> AppliedAsync(NpgsqlConnection connection) =>
        await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM identity.\"" + StoreContext.MigrationsHistoryTable + "\"");

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
                "INSERT INTO identity.accounts (subject, created_at, state) VALUES (@subject, now(), 'pending')",
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
            new { schema = StoreContext.Schema });

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
                "INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key) "
                    + "VALUES (@subject, 1, 1, @key)",
                new { subject = Guid.NewGuid(), key = new byte[32] }));

        Assert.Equal("ck_subject_keys_format", refusal.ConstraintName);
    }
}
