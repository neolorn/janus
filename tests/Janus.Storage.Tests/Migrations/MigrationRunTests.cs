using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Migrations;

/// <summary>
/// A run of the migrations as the pipeline makes it, by the migration tool in a process
/// of its own, over a database one migration refuses (OPS-MIG-001, OPS-MIG-002).
/// </summary>
/// <param name="database">The instance the database is created on.</param>
[Trait("kind", "integration")]
public sealed class MigrationRunTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    // The migration that refuses where a value of no subject is bound the old way, and
    // the one before it.
    private const string BeforeTheRefusal = "20260929142007_MoveValuesUnderTheDeploymentKey";

    private const string TheRefusal = "20260929170223_HoldTheDeploymentKeyUnderTheMaxUuid";

    /// <summary>
    /// OPS-MIG-002 AC1: a run in which one migration fails exits with a non-zero code,
    /// and the history holds every migration before the failing one and nothing from it
    /// on, so the pipeline stops before a deploy with nothing half applied.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_002_AC1_AFailedRunExitsNonZeroAndAppliesNothingAfterTheFailingMigrationAsync()
    {
        string refusing = await database.CreateDatabaseAsync("failing_run");
        IReadOnlyList<string> declared;

        await using (StoreContext context = DatabaseFixture.Context(refusing))
        {
            declared = [.. context.Database.GetMigrations()];
            await context.GetService<IMigrator>().MigrateAsync(BeforeTheRefusal, TestContext.Current.CancellationToken);
        }

        await using (var connection = new NpgsqlConnection(refusing))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.send_outbox (id, recorded_at, wrapped_key, enc_message)
                VALUES (gen_random_uuid(), now(), '\x01', '\x02');
                """);
        }

        (int exit, string written) = await RunAsync(refusing);

        await using var reading = new NpgsqlConnection(refusing);
        await reading.OpenAsync(TestContext.Current.CancellationToken);

        IEnumerable<string> applied = await reading.QueryAsync<string>(
            "SELECT \"MigrationId\" FROM identity." + StoreContext.MigrationsHistoryTable + " ORDER BY \"MigrationId\"");

        Assert.NotEqual(0, exit);
        Assert.Contains("identity.send_outbox holds values not bound to their own rows.", written, StringComparison.Ordinal);
        Assert.Equal(declared.TakeWhile(migration => migration != TheRefusal), applied);
        Assert.NotEmpty(declared.SkipWhile(migration => migration != TheRefusal).Skip(1));
    }

    // The migration tool the pipeline runs, over the build this test was built with.
    private static async Task<(int Exit, string Written)> RunAsync(string connectionString)
    {
        var start = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } named ? named : "dotnet")
        {
            WorkingDirectory = Repository.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in new[]
        {
            "ef",
            "database",
            "update",
            "--project",
            "src/Janus.Storage",
            "--configuration",
            typeof(MigrationRunTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration,
            "--no-build",
            "--connection",
            connectionString,
        })
        {
            start.ArgumentList.Add(argument);
        }

        using Process run = Process.Start(start)
            ?? throw new InvalidOperationException("The migration tool did not start.");

        Task<string> output = run.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = run.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await run.WaitForExitAsync(TestContext.Current.CancellationToken);

        return (run.ExitCode, await output + await error);
    }
}
