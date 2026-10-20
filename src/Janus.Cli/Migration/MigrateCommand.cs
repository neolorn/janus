using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Migration;

/// <summary>
/// The <c>migrate</c> command: applies the library's migrations to the database of the
/// connection piped to it, or with <c>--report</c> reports what they would do and
/// applies nothing.
/// </summary>
/// <remarks>
/// Implements OPS-MIG-001, CONV-DESIGN-003 and CONV-DESIGN-007, as D-204 and D-206 settle
/// them. The document carries the migration connection alone and the command reads no
/// key, so the pipeline's migration step never holds the key-encryption key (D-173). It
/// runs no schema check, which needs the schema it creates, and none of the model's
/// checks, which need the host's declaration; a run that applied every migration owed
/// writes nothing. The report exits 0 whatever it found: whether a destructive statement
/// stops a deploy is the pipeline's gate (OPS-DEP-001).
/// </remarks>
internal static class MigrateCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "migrate";

    /// <summary>
    /// The argument that asks for the destructive-operation report in place of the run.
    /// </summary>
    public const string Report = "--report";

    /// <summary>
    /// What the command declares (LIB-API-001): the report as its one argument, the
    /// connection alone, nothing written after a run and the report's lines after a
    /// report, and the codes a refusal carries.
    /// </summary>
    public static CommandContract Contract { get; } = new(
        Name,
        [new(Report, Required: false, "flag")],
        KeyDocument.ConnectionMembers,
        [new("application/jsonl", ["migration: string", "statement: string", "destructive: boolean"], Report)],
        [ErrorCodes.RequestMalformed, ErrorCodes.StartupSecretUnavailable, ErrorCodes.SystemFault]);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name: nothing, or <c>--report</c>.</param>
    /// <param name="terminal">Where the connection is read from.</param>
    /// <param name="cancellationToken">Abandons the command; the migration in hand rolls back.</param>
    /// <returns>
    /// Nothing to write after a run, the report's lines after a report, or the failure
    /// naming what was refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        bool reported = arguments is [Report];

        if (arguments.Count is not 0 && !reported)
        {
            return Result.Failure<string>(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(arguments[arguments[0] == Report ? 1 : 0])));
        }

        Error? failure = null;

        string connection = (await KeyDocument.ConnectionAsync(terminal, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        await using ServiceProvider services = Composed(connection);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        LibraryMigrations migrations = scope.ServiceProvider.GetRequiredService<LibraryMigrations>();

        if (reported)
        {
            return (await migrations.PendingStatementsAsync(cancellationToken).ConfigureAwait(false))
                .Match(
                    statements => Result.Success(DestructiveOperationReport.Of(
                        [.. statements.Select(pending => (pending.Migration, pending.Statement))],
                        terminal.Output.NewLine)),
                    Result.Failure<string>);
        }

        return (await migrations.ApplyAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(string.Empty), Result.Failure<string>);
    }

    /// <summary>
    /// What the command runs over: the storage area under the connection and the
    /// migrations it applies, and no key ring (CONV-DESIGN-007).
    /// </summary>
    /// <param name="connection">The migration connection.</param>
    /// <returns>The container the command resolves from.</returns>
    internal static ServiceProvider Composed(string connection)
    {
        var services = new ServiceCollection();

        services.AddStorageArea(connection);
        services.AddScoped<LibraryMigrations>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
