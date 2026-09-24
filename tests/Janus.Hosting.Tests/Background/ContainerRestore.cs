using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Janus.Core;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// What a deployment's restore does, over containers: a new PostgreSQL instance of the
/// version the deployment runs, with a backup of the fixture's instance replayed into it,
/// and torn down when asked.
/// </summary>
/// <param name="backup">The backup, taken from the running instance before the test.</param>
/// <param name="database">The database the library's tables live in.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class ContainerRestore(byte[] backup, string database) : IRestoreTestInstance, IAsyncDisposable
{
    private const string Script = "/tmp/backup.sql";

    private PostgreSqlContainer? _instance;

    /// <summary>
    /// How the restored database was reached, once a restore has run.
    /// </summary>
    public string? Restored { get; private set; }

    /// <summary>
    /// How many times a teardown was asked for.
    /// </summary>
    public int TornDown { get; private set; }

    /// <inheritdoc/>
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

        Restored = new NpgsqlConnectionStringBuilder(_instance.GetConnectionString())
        {
            Database = database,
        }.ConnectionString;

        return Result.Success(Restored);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> TearDownAsync(CancellationToken cancellationToken)
    {
        TornDown++;

        await DisposeAsync();

        return Result.Success();
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
