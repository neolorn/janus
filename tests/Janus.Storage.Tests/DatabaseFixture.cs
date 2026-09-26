using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DotNet.Testcontainers.Containers;
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
    private const string Database = "identity";

    private const string BackupScript = "/tmp/backup.sql";

    // The cases stamp their records in September 2026 and the weeks after it, the months
    // a migration run then created. The migration creates the months of the day it runs,
    // so a later run is given these as that one created them, under the names the
    // migration's function gives them.
    private const string FixedMonths =
        """
        DO $months$
        DECLARE
            parent text;
            starts timestamp with time zone;
        BEGIN
            FOREACH parent IN ARRAY ARRAY['audit_records_security', 'audit_records_routine'] LOOP
                FOREACH starts IN ARRAY ARRAY[
                    timestamptz '2026-09-01 00:00:00+00',
                    timestamptz '2026-10-01 00:00:00+00',
                    timestamptz '2026-11-01 00:00:00+00'] LOOP
                    EXECUTE format(
                        'CREATE TABLE IF NOT EXISTS identity.%I PARTITION OF identity.%I '
                            || 'FOR VALUES FROM (%L) TO (%L)',
                        parent || '_' || to_char(starts AT TIME ZONE 'UTC', 'YYYY_MM'),
                        parent,
                        starts,
                        starts + interval '1 month');
                END LOOP;
            END LOOP;
        END
        $months$;
        """;

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

    /// <summary>
    /// Takes a backup of the whole instance, its roles and every database, as the script
    /// that replays it into a new instance.
    /// </summary>
    /// <returns>The script.</returns>
    /// <exception cref="InvalidOperationException">The backup could not be taken.</exception>
    public async ValueTask<byte[]> BackupAsync()
    {
        string superuser = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()).Username
            ?? throw new InvalidOperationException("The instance names no superuser.");

        ExecResult taken = await _container.ExecAsync(
            ["pg_dumpall", "--username", superuser, "--file", BackupScript],
            TestContext.Current.CancellationToken);

        return taken.ExitCode == 0
            ? await _container.ReadFileAsync(BackupScript, TestContext.Current.CancellationToken)
            : throw new InvalidOperationException("The backup could not be taken.");
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

        await using NpgsqlConnection connection = await OpenAsync();
        await connection.ExecuteAsync(FixedMonths);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
