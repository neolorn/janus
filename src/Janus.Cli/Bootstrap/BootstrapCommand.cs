using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Bootstrap;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Bootstrap;

/// <summary>
/// The <c>bootstrap</c> command: stands a fresh deployment up and prints the first
/// administrator's enrolment link.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001, OPS-SEC-001 and D-028. The command needs only the
/// database: the keys come from standard input, the values from the arguments, and
/// nothing is sent anywhere, so the link is printed because the mailbox it would
/// otherwise reach is only queued. Everything a refusal can be told from is checked
/// before the database is reached, and a schema the migrations have not brought
/// current is refused before anything is written to it.
/// </remarks>
internal static class BootstrapCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "bootstrap";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>The full <c>/enrol</c> address, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        Error? failure = null;

        BootstrapRequest request = BootstrapArguments.Read(arguments)
            .Match(value => value, error => Withheld<BootstrapRequest>(error, ref failure));

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

        return (await scope.ServiceProvider.GetRequiredService<DeploymentBootstrap>()
                .RunAsync(request, cancellationToken)
                .ConfigureAwait(false))
            .Match(enrolment => Result.Success(enrolment.Address.AbsoluteUri), Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the service that seeds the deployment.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddStorageArea(keys.Connection, keys.KeyEncryptionKeys, keys.FingerprintKey);
        services.AddScoped<SchemaValidation>();
        services.AddScoped<DeploymentBootstrap>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
