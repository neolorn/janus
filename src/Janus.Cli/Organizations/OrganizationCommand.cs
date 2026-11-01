using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Organizations;

/// <summary>
/// The <c>organization</c> command: sets one of the deployment's organizations up from
/// the server, by name, and prints its identifier.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001, IDN-ORG-002, OPS-SEC-001 and D-248. A deployment standing up
/// production needs its business organization before any person can work, and the
/// policy route needs a person signed in with a passed step-up, so the access the set-up
/// needs is the server's, as <c>bootstrap</c>'s is. Everything a refusal can be told from
/// is checked before the database is reached, the policy file among it, and the set-up is
/// one transaction.
/// </remarks>
internal static class OrganizationCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "organization";

    // The argument the policy file is named in, which a refusal of the file names.
    private const string Policy = "policy";

    /// <summary>
    /// What the command declares (LIB-API-001): its arguments, the key document, the
    /// organization's identifier on one line, and the codes a refusal or a fault carries,
    /// the policy route's among them.
    /// </summary>
    public static CommandContract Contract { get; } = new(
        Name,
        OrganizationArguments.Declared,
        KeyDocument.Members,
        [new("text/plain", ["organization: string"])],
        [
            ErrorCodes.RequestMalformed,
            ErrorCodes.Denied,
            ErrorCodes.ConfigurationChangeReasonRequired,
            ErrorCodes.ConfigurationPolicyBelowSystem,
            ErrorCodes.ConfigurationValueBelowFloor,
            ErrorCodes.ConfigurationValueNotAllowed,
            ErrorCodes.IdentifierMixedScript,
            ErrorCodes.StartupSchemaMismatch,
            ErrorCodes.StartupSecretUnavailable,
            ErrorCodes.SystemFault,
        ]);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>The organization's identifier, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        Error? failure = null;

        OrganizationRequest request = OrganizationArguments.Read(arguments)
            .Match(value => value, error => Withheld<OrganizationRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        PolicyOverride policy = (await PolicyAsync(request.Policy, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<PolicyOverride>(error, ref failure));

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

        return (await scope.ServiceProvider.GetRequiredService<OrganizationFromServer>()
                .RunAsync(
                    new OrganizationFromServerRequest(request.Name, policy, request.Reason, request.MultipleMemberships),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(organization => Result.Success(organization.ToString()), Result.Failure<string>);
    }

    // OPS-BOOT-001 and D-248: the file holds the policy object the policy route takes,
    // without the reason the command line gives. A file that does not read or does not
    // hold that object, a member it does not have among them, is refused as the argument
    // that named it, and a value a field does not take is refused with the route's code,
    // naming it too.
    private static async Task<Result<PolicyOverride>> PolicyAsync(string path, CancellationToken cancellationToken)
    {
        JsonDocument document;

        try
        {
            await using FileStream file = File.OpenRead(path);

            document = await JsonDocument.ParseAsync(file, default, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception unreadable)
            when (unreadable is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            return Result.Failure<PolicyOverride>(Malformed());
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return Result.Failure<PolicyOverride>(Malformed());
            }

            return PolicyObject.Written(document.RootElement, FrozenSet<string>.Empty)
                .Match(
                    fields => Settings.OrganizationPolicy.EnteredIn(Policy, fields),
                    _ => Result.Failure<PolicyOverride>(Malformed()));
        }
    }

    private static Error Malformed() =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(Policy));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, the authentication area, whose event outbox, alert raising
    // and policy resolution the set-up writes through, and the set-up itself.
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
        services.AddScoped<OrganizationFromServer>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
