using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What <c>migrate</c> does to a database: the migrations owed applied once, the step the
/// pipeline runs before a deploy, and nothing applied on a refusal (OPS-MIG-001, D-204).
/// </summary>
/// <param name="database">The instance each case creates its database on.</param>
[Trait("kind", "integration")]
public sealed class MigrateTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Migrate = "migrate";

    // The migration that refuses where a value of no subject is bound the old way, and
    // the one before it.
    private const string BeforeTheRefusal = "20260929142007_MoveValuesUnderTheDeploymentKey";

    private const string TheRefusal = "20260929170223_HoldTheDeploymentKeyUnderTheMaxUuid";

    /// <summary>
    /// OPS-MIG-001 AC3, AC4: a document carrying the connection alone is accepted; the
    /// first run applies every migration the build declares, and a second run against the
    /// same database applies none and changes nothing. Each run exits 0 with nothing on
    /// standard output, and neither checks the schema it creates.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_001_AC3_ASecondRunAppliesNothingAndChangesNothingAsync()
    {
        string migrated = await database.CreateDatabaseAsync("migrated_twice");

        Invocation first = await Invocation.PipedAsync([Migrate], Connection(migrated));
        IReadOnlyList<string> afterTheFirst = await AppliedAsync(migrated);
        IReadOnlyList<string> schemaAfterTheFirst = await SchemaAsync(migrated);

        Invocation second = await Invocation.PipedAsync([Migrate], Connection(migrated));

        Reported.Success(first);
        Reported.Success(second);
        Assert.Empty(first.Output);
        Assert.Empty(second.Output);
        Assert.Equal(DatabaseFixture.DeclaredMigrations(), afterTheFirst);
        Assert.Equal(afterTheFirst, await AppliedAsync(migrated));
        Assert.Equal(schemaAfterTheFirst, await SchemaAsync(migrated));
    }

    /// <summary>
    /// OPS-MIG-001 AC4: a document carrying any member beside the connection, or in place
    /// of it, is refused as malformed naming the input, and nothing is applied.
    /// </summary>
    /// <param name="name">The database the case creates.</param>
    /// <param name="withConnection">Whether the document carries the connection.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("refused_with_keys", true)]
    [InlineData("refused_keys_alone", false)]
    public async Task OPS_MIG_001_AC4_ADocumentCarryingAKeyAppliesNothingAsync(string name, bool withConnection)
    {
        string untouched = await database.CreateDatabaseAsync(name);
        JsonObject document = Invocation.Keys(untouched);

        if (!withConnection)
        {
            Assert.True(document.Remove("connection"));
        }

        Invocation run = await Invocation.PipedAsync([Migrate], document);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Empty(await SchemaAsync(untouched));
    }

    /// <summary>
    /// OPS-MIG-001 AC2: a run in which the database refuses a migration exits 1 with one
    /// JSON line <c>system.fault</c> on standard error, carrying nothing of what the
    /// database said, and the history holds every migration before the refused one and
    /// nothing from it on, so the pipeline stops before a deploy with nothing half
    /// applied.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_001_AC2_ARefusedMigrationFailsTheRunAndNothingAfterItIsAppliedAsync()
    {
        string refusing = await database.CreateDatabaseAsync("failing_run");
        IReadOnlyList<string> declared = DatabaseFixture.DeclaredMigrations();

        await DatabaseFixture.MigrateAsync(refusing, BeforeTheRefusal);

        await using (var connection = new NpgsqlConnection(refusing))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.send_outbox (id, recorded_at, wrapped_key, enc_message)
                VALUES (gen_random_uuid(), now(), '\x01', '\x02');
                """);
        }

        Invocation run = await Invocation.PipedAsync([Migrate], Connection(refusing));

        using var refusal = JsonDocument.Parse(run.Error);

        Assert.Equal(ErrorCodes.SystemFault.ToString(), Reported.Refusal(run));
        Assert.Empty(refusal.RootElement.GetProperty("details").EnumerateObject());
        Assert.DoesNotContain("send_outbox", run.Error, StringComparison.Ordinal);
        Assert.Equal(declared.TakeWhile(migration => migration != TheRefusal), await AppliedAsync(refusing));
        Assert.NotEmpty(declared.SkipWhile(migration => migration != TheRefusal).Skip(1));
    }

    private static JsonObject Connection(string connectionString) => new() { ["connection"] = connectionString };

    // The history, in the order the migrations applied.
    private static async Task<IReadOnlyList<string>> AppliedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return [.. await connection.QueryAsync<string>(
            "SELECT \"MigrationId\" FROM identity.__migrations_history ORDER BY \"MigrationId\"")];
    }

    // Every relation, column and function of the database outside the system's schemas.
    private static async Task<IReadOnlyList<string>> SchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return [.. await connection.QueryAsync<string>(
            """
            SELECT n.nspname || '.' || c.relname || ':' || c.relkind::text
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%'
            UNION ALL
            SELECT table_schema || '.' || table_name || '.' || column_name || ':' || data_type
            FROM information_schema.columns
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            UNION ALL
            SELECT n.nspname || '.' || p.proname || '()'
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1
            """)];
    }
}
