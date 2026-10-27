using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Janus.Storage;

/// <summary>
/// The library's migrations, applied to the database of the connection the context was
/// given.
/// </summary>
/// <param name="context">The context whose declared migrations are applied.</param>
/// <remarks>
/// Implements OPS-MIG-001 and CONV-DESIGN-003. Each migration applies in a transaction of
/// its own, so a migration the database refuses leaves the ones before it applied and
/// nothing of itself or after it; a migration already in the history is not applied
/// again. Nothing here reads a key or checks the schema the run creates.
/// </remarks>
internal sealed class LibraryMigrations(StoreContext context)
{
    /// <summary>
    /// Applies every migration the history does not hold.
    /// </summary>
    /// <param name="cancellationToken">Abandons the run; the migration in hand rolls back.</param>
    /// <returns>Nothing, or <c>system.fault</c> where the database refused.</returns>
    public async ValueTask<Result> ApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (DbException)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

    /// <summary>
    /// Reads the statements of every migration the history does not hold, from the
    /// idempotent script EF Core's migrator writes of them, and applies nothing.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// Each statement with its migration, in the order the migrations apply; or
    /// <c>system.fault</c> where the database could not be read.
    /// </returns>
    /// <remarks>
    /// Implements OPS-MIG-001 AC5 and CONV-DESIGN-003, as D-206 settles them. The
    /// migrations owed are read from the history and apply in the order they are
    /// declared, one dated before a migration already applied included, so the script is
    /// written for each run of them from the migration before it. The script guards each
    /// statement with a block naming its migration; the block is the history's own, so
    /// it is found as the history writes it and never read for a statement.
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<PendingStatement>>> PendingStatementsAsync(
        CancellationToken cancellationToken)
    {
        HashSet<string> pending;

        try
        {
            pending =
            [
                .. await context.Database
                    .GetPendingMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false),
            ];
        }
        catch (DbException)
        {
            return Result.Failure<IReadOnlyList<PendingStatement>>(Error.From(ErrorCodes.SystemFault));
        }

        IMigrator migrator = context.GetService<IMigrator>();
        IHistoryRepository history = context.GetService<IHistoryRepository>();
        string end = history.GetEndIfScript().ReplaceLineEndings("\n");
        var statements = new List<PendingStatement>();
        string previous = Migration.InitialDatabase;
        var run = new List<string>();

        foreach (string migration in context.Database.GetMigrations().Append(string.Empty))
        {
            if (pending.Contains(migration))
            {
                run.Add(migration);
                continue;
            }

            if (run.Count is not 0)
            {
                string script = migrator
                    .GenerateScript(previous, run[^1], MigrationsSqlGenerationOptions.Idempotent)
                    .ReplaceLineEndings("\n");

                statements.AddRange(Guarded(script, run, history, end));
                run.Clear();
            }

            previous = migration;
        }

        return Result.Success<IReadOnlyList<PendingStatement>>(statements);
    }

    // The statements of each block of the script that guards one of the run's
    // migrations, block by block in the order the script runs them.
    private static IEnumerable<PendingStatement> Guarded(
        string script,
        IReadOnlyList<string> run,
        IHistoryRepository history,
        string end)
    {
        var blocks = new SortedDictionary<int, (string Migration, int Body)>();

        foreach (string migration in run)
        {
            string begin = history.GetBeginIfNotExistsScript(migration).ReplaceLineEndings("\n");

            for (int at = script.IndexOf(begin, StringComparison.Ordinal);
                at >= 0;
                at = script.IndexOf(begin, at + begin.Length, StringComparison.Ordinal))
            {
                blocks[at] = (migration, at + begin.Length);
            }
        }

        foreach ((string migration, int body) in blocks.Values)
        {
            int closed = script.IndexOf(end, body, StringComparison.Ordinal);

            if (closed < 0)
            {
                throw new InvalidOperationException($"The script's block for {migration} is never closed.");
            }

            foreach (string statement in ScriptStatements.Of(script[body..closed]))
            {
                yield return new PendingStatement(migration, statement);
            }
        }
    }
}
