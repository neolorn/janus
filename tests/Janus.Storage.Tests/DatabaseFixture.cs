using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// A PostgreSQL of the version the deployment runs, with the library's database created
/// under the ICU locale before any migration touches it, and the migrations applied.
/// </summary>
/// <remarks>
/// Implements OPS-DB-001 and OPS-MIG-007. One container per test class, torn down with
/// the class (CONV-TEST-002, CONV-TEST-007).
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string Database = "janus";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>
    /// How to reach the library's database.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Opens a context over the library's database.
    /// </summary>
    /// <returns>The context.</returns>
    internal StoreContext Context() =>
        new(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(
                StoreContext.MigrationsHistoryTable,
                StoreContext.Schema))
            .Options);

    /// <summary>
    /// Applies the migrations to the library's database.
    /// </summary>
    /// <returns>The work of applying them.</returns>
    public async ValueTask MigrateAsync()
    {
        await using StoreContext context = Context();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Opens a connection to the library's database for a hand-written query.
    /// </summary>
    /// <returns>The open connection.</returns>
    public async ValueTask<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(CancellationToken.None);

        // OPS-DB-001 AC3: the locale belongs to the database, so it is set when the
        // database is created and before the first migration runs.
        await using (var maintenance = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await maintenance.OpenAsync(CancellationToken.None);
            await maintenance.ExecuteAsync(
                "CREATE DATABASE " + Database + " TEMPLATE template0 "
                    + "LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'");
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = Database,
        }.ConnectionString;

        await MigrateAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
