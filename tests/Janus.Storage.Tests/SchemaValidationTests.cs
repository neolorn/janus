using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What the check that runs before anything is served decides about the database it
/// was pointed at (OPS-MIG-002, OPS-MIG-001).
/// </summary>
/// <param name="database">The migrated database of this build.</param>
[Trait("kind", "integration")]
public sealed class SchemaValidationTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Behind = "unmigrated";

    /// <summary>
    /// OPS-MIG-002 AC1: the database the pipeline migrated carries the schema this
    /// build was compiled against, so the check passes and the application starts.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_002_AC1_TheMigratedDatabaseMatchesTheModelAsync()
    {
        await using StoreContext context = database.Context();

        Result read = await new SchemaValidation(context)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Null(read.Match(() => (Error?)null, failure => failure));
    }

    /// <summary>
    /// OPS-MIG-002 AC1: a database the pipeline did not migrate answers with the
    /// library's own code and names every migration it still owes.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_002_AC1_ADatabaseBehindTheModelIsRefusedByNameAsync()
    {
        await using StoreContext context = await BehindAsync();

        Result read = await new SchemaValidation(context)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Error? refusal = read.Match(() => (Error?)null, failure => failure);

        Assert.Equal(ErrorCodes.StartupSchemaMismatch, refusal?.Code);
        Assert.Equal(
            context.Database.GetMigrations(),
            refusal?.Details["pending"].EnumerateArray().Select(name => name.GetString()));
    }

    /// <summary>
    /// OPS-MIG-001 AC1: the check reads the history and applies nothing, so the
    /// database it refused is as un-migrated afterwards as it was before.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_001_AC1_TheCheckLeavesTheDatabaseUnmigratedAsync()
    {
        await using StoreContext context = await BehindAsync();

        _ = await new SchemaValidation(context)
            .ValidateAsync(TestContext.Current.CancellationToken);

        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        IEnumerable<string> schemas = await connection.QueryAsync<string>(
            "SELECT nspname FROM pg_namespace WHERE nspname = @schema",
            new { schema = StoreContext.Schema });

        Assert.Empty(schemas);
    }

    /// <summary>
    /// OPS-MIG-002 AC3: a history that holds one migration more than the build declares
    /// is what the previous version meets during a rollout, so the check passes and the
    /// application starts.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_002_AC3_ADatabaseOneMigrationAheadOfTheBuildStartsAsync()
    {
        const string ahead = "99991231235959_AddedByTheNextVersion";

        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            "INSERT INTO identity." + StoreContext.MigrationsHistoryTable + " (\"MigrationId\", \"ProductVersion\") "
                + "SELECT @ahead, max(\"ProductVersion\") FROM identity." + StoreContext.MigrationsHistoryTable,
            new { ahead });

        try
        {
            await using StoreContext context = database.Context();

            Result read = await new SchemaValidation(context)
                .ValidateAsync(TestContext.Current.CancellationToken);

            Assert.DoesNotContain(ahead, context.Database.GetMigrations());
            Assert.Contains(
                ahead,
                await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
            Assert.Null(read.Match(() => (Error?)null, failure => failure));
        }
        finally
        {
            // The other cases of the class read the same history, so the row goes out
            // with the case that wrote it.
            await connection.ExecuteAsync(
                "DELETE FROM identity." + StoreContext.MigrationsHistoryTable + " WHERE \"MigrationId\" = @ahead",
                new { ahead });
        }
    }

    // A second database on the same server, created as the deployment's own is and
    // never migrated: the state OPS-MIG-002 is about.
    private async ValueTask<StoreContext> BehindAsync()
    {
        await using (NpgsqlConnection server = await database.OpenAsync())
        {
            IEnumerable<string> present = await server.QueryAsync<string>(
                "SELECT datname FROM pg_database WHERE datname = @name",
                new { name = Behind });

            if (!present.Any())
            {
                await server.ExecuteAsync(
                    "CREATE DATABASE " + Behind + " TEMPLATE template0 "
                        + "LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'");
            }
        }

        return new StoreContext(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(
                new NpgsqlConnectionStringBuilder(database.ConnectionString)
                {
                    Database = Behind,
                }.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    StoreContext.MigrationsHistoryTable,
                    StoreContext.Schema))
            .Options);
    }
}
