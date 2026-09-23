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
        await using JanusDbContext context = database.Context();

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
        await using JanusDbContext context = await BehindAsync();

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
        await using JanusDbContext context = await BehindAsync();

        _ = await new SchemaValidation(context)
            .ValidateAsync(TestContext.Current.CancellationToken);

        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        IEnumerable<string> schemas = await connection.QueryAsync<string>(
            "SELECT nspname FROM pg_namespace WHERE nspname = @schema",
            new { schema = JanusDbContext.Schema });

        Assert.Empty(schemas);
    }

    // A second database on the same server, created as the deployment's own is and
    // never migrated: the state OPS-MIG-002 is about.
    private async ValueTask<JanusDbContext> BehindAsync()
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

        return new JanusDbContext(new DbContextOptionsBuilder<JanusDbContext>()
            .UseNpgsql(
                new NpgsqlConnectionStringBuilder(database.ConnectionString)
                {
                    Database = Behind,
                }.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    JanusDbContext.MigrationsHistoryTable,
                    JanusDbContext.Schema))
            .Options);
    }
}
