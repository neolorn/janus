using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Migration;

/// <summary>
/// The <c>migrate</c> command: applies the library's migrations to the database of the
/// connection piped to it.
/// </summary>
/// <remarks>
/// Implements OPS-MIG-001, CONV-DESIGN-003 and CONV-DESIGN-007, as D-204 settles them. The
/// document carries the migration connection alone and the command reads no key, so the
/// pipeline's migration step never holds the key-encryption key (D-173). It runs no
/// schema check, which needs the schema it creates, and none of the model's checks, which
/// need the host's declaration; a run that applied every migration owed writes nothing.
/// </remarks>
internal static class MigrateCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "migrate";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name, of which it takes none.</param>
    /// <param name="terminal">Where the connection is read from.</param>
    /// <param name="cancellationToken">Abandons the command; the migration in hand rolls back.</param>
    /// <returns>Nothing to write, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        if (arguments.Count is not 0)
        {
            return Result.Failure<string>(
                Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(arguments[0])));
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

        return (await scope.ServiceProvider.GetRequiredService<LibraryMigrations>()
                .ApplyAsync(cancellationToken)
                .ConfigureAwait(false))
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
