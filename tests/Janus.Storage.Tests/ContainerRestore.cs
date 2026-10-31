using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Janus.Core;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Janus.Storage.Tests;

/// <summary>
/// What a deployment's restore does, over containers: a new PostgreSQL instance of the
/// version the deployment runs, with a backup of the fixture's instance replayed into it,
/// and torn down when disposed.
/// </summary>
/// <param name="backup">The backup, taken from the running instance before the test.</param>
/// <param name="database">The database the library's tables live in.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
public sealed class ContainerRestore(byte[] backup, string database) : IAsyncDisposable
{
    private const string Script = "/tmp/backup.sql";

    private PostgreSqlContainer? _instance;

    /// <summary>
    /// Builds a new instance and replays the backup into it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the restore.</param>
    /// <returns>How to reach the restored database, or the failure where nothing was restored.</returns>
    public async ValueTask<Result<string>> RestoreAsync(CancellationToken cancellationToken)
    {
        _instance = new PostgreSqlBuilder("postgres:17-alpine").Build();

        await _instance.StartAsync(cancellationToken);
        await _instance.CopyAsync(backup, Script, ct: cancellationToken);

        string superuser = new NpgsqlConnectionStringBuilder(_instance.GetConnectionString()).Username
            ?? throw new InvalidOperationException("The instance names no superuser.");

        ExecResult replayed = await _instance.ExecAsync(
            ["psql", "--username", superuser, "--dbname", "postgres", "--quiet", "--file", Script],
            cancellationToken);

        if (replayed.ExitCode != 0)
        {
            return Result.Failure<string>(Error.From(ErrorCodes.SystemFault));
        }

        return Result.Success(new NpgsqlConnectionStringBuilder(_instance.GetConnectionString())
        {
            Database = database,
        }.ConnectionString);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_instance is not null)
        {
            await _instance.DisposeAsync();
            _instance = null;
        }
    }
}
