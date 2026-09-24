using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Configuration;

/// <summary>
/// The <c>configure</c> command: changes protected keys from the server, each change
/// written down under the command's principal and raised.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-004 AC2, D-071 and OPS-SEC-001, as entry 319 of the decisions
/// pending review settles them. The application refuses every protected key, so the
/// access a change needs is the server's: the document piped to the command, which
/// names the connection it runs under. Everything a refusal can be told from is checked
/// before the database is reached, and the change is one transaction.
/// </remarks>
internal static class ConfigureCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "configure";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>The keys changed, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        Error? failure = null;

        ConfigureRequest request = ConfigureArguments.Read(arguments)
            .Match(value => value, error => Withheld<ConfigureRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        using var held = new HeldKeys();

        KeyDocument keys = (await KeyDocument.ReadAsync(terminal, held, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<KeyDocument>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        await using ServiceProvider services = Composed(keys);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        failure = (await scope.ServiceProvider.GetRequiredService<SchemaValidation>()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        return (await scope.ServiceProvider.GetRequiredService<ProtectedConfiguration>()
                .ChangeAsync(request.Values, request.Reason, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(Report(request.Values)), Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The keys changed, in the order named; nothing of their values.
    private static string Report(IReadOnlyList<ProtectedValue> values) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["changed"] = values.Select(value => value.Key.ToString()).ToArray(),
            }));

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the change itself.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddStorageArea(keys.Connection, keys.KeyEncryptionKeys, keys.FingerprintKeys);
        services.AddScoped<SchemaValidation>();
        services.AddScoped<ProtectedConfiguration>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
