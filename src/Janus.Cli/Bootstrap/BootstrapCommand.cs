using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Bootstrap;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Bootstrap;

/// <summary>
/// The <c>bootstrap</c> command: stands a fresh deployment up and prints the first
/// administrator's enrolment link and the administrative organization's identifier.
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
    /// What the command declares (LIB-API-001): its arguments, the key document, the
    /// enrolment address and the administrative organization's identifier on two lines
    /// (D-204), and the codes a refusal carries.
    /// </summary>
    public static CommandContract Contract { get; } = new(
        Name,
        BootstrapArguments.Declared,
        KeyDocument.Members,
        [
            new("text/plain", ["enrolment: string"]),
            new("text/plain", ["organization: string"]),
        ],
        [
            ErrorCodes.RequestMalformed,
            ErrorCodes.Denied,
            ErrorCodes.ConfigurationValueNotAllowed,
            ErrorCodes.IdentifierInvalid,
            ErrorCodes.IdentifierMixedScript,
            ErrorCodes.ProfileUnderage,
            ErrorCodes.StartupDeclarationMissing,
            ErrorCodes.StartupGoverningLanguage,
            ErrorCodes.StartupRelyingPartyId,
            ErrorCodes.StartupSchemaMismatch,
            ErrorCodes.StartupSecretUnavailable,
        ]);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>
    /// Two lines, the full <c>/enrol</c> address and the administrative organization's
    /// identifier (D-204); or the failure naming what was refused.
    /// </returns>
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
            .Match(
                enrolment => Result.Success(enrolment.Address.AbsoluteUri + terminal.Output.NewLine + enrolment.Organization),
                Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, the authentication area, whose event outbox and alert
    // channels the seeding writes through, and the service that seeds the deployment.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);

        // CONV-DESIGN-007, CONV-CODE-007: the ring the document was read into stands in
        // the place of the one the core registers, so every service of the command
        // borrows from the ring the command filled.
        services.AddCoreArea();
        services.AddSingleton(keys.Ring);
        services.AddStorageArea(keys.Connection);
        services.AddAuthenticationArea();
        services.AddScoped<DeploymentBootstrap>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
