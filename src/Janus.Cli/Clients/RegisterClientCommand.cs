using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Clients;

/// <summary>
/// The <c>register-client</c> command: registers a client in the provider's registry,
/// or carries a change to one, from the server.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, OPS-SEC-001 and OPS-SEC-002, as entry 340 of the decisions
/// pending review settles them. The registry has no endpoint, so the access a
/// registration needs is the server's: the document piped to the command, which
/// carries the client's secret beside the keys, so the secret is never an argument.
/// Registering a client again with a new secret leaves the one it replaced accepted
/// for the overlap, which is how a secret is rotated. Everything a refusal can be told
/// from is checked before the database is reached.
/// </remarks>
internal static class RegisterClientCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "register-client";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys and the secret are read from.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>The client registered, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        Error? failure = null;

        OidcClient client = RegisterClientArguments.Read(arguments)
            .Match(value => value, error => Withheld<OidcClient>(error, ref failure));

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

        if (keys.ClientSecret is not ReadOnlyMemory<byte> secret)
        {
            return Result.Failure<string>(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(KeyDocument.ClientSecretMember)));
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

        return (await scope.ServiceProvider.GetRequiredService<ClientRegistry>()
                .RegisterAsync(client, secret, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(Report(client)), Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The client registered; nothing of its secret.
    private static string Report(OidcClient client) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["registered"] = client.ClientId,
            }));

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the registry itself.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddStorageArea(keys.Connection, keys.KeyEncryptionKeys, keys.FingerprintKeys);
        services.AddScoped<SchemaValidation>();
        services.AddScoped<ClientRegistry>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
