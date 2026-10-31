using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy;
using Janus.Privacy.SubjectKeys;
using Janus.Storage;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Rotation;

/// <summary>
/// The <c>rotate-fingerprint-key</c> command: computes every stored fingerprint under a
/// previous version of the fingerprint key again under its current version, or, given
/// <c>--retire</c>, retires the versions before it.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 AC6 and OPS-SEC-001, in the shape of <c>rotate-kek</c> (D-166,
/// D-225). The operator puts the new version in the secrets
/// manager as current, keeping the previous one, restarts the application on it, and
/// pipes the document to the command under the maintenance credential. A run that stops
/// is resumed by running it again.
/// </remarks>
internal static class RotateFingerprintKeyCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "rotate-fingerprint-key";

    /// <summary>
    /// What the command declares (LIB-API-001): its arguments, the key document, the
    /// pass's report, or with the retirement its report, and the codes a refusal or a
    /// fault carries.
    /// </summary>
    public static CommandContract Contract { get; } = new(
        Name,
        RotationArguments.Declared,
        KeyDocument.Members,
        [
            new("application/json", ["version: number", "processed: number"]),
            new(
                "application/json",
                ["version: number", "processed: number", "retired[]: number"],
                RotationArguments.Retire),
        ],
        [
            ErrorCodes.RequestMalformed,
            ErrorCodes.Denied,
            ErrorCodes.RotationNotReady,
            ErrorCodes.StartupSecretUnavailable,
            ErrorCodes.SystemFault,
        ]);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">
    /// Abandons the command; the batch in hand rolls back and the next run resumes after
    /// the last one that committed.
    /// </param>
    /// <returns>
    /// The rotation's version and the count of fingerprints it computed again, with the
    /// versions retired where the retirement was asked for; or the failure naming what was
    /// refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        bool retire = false;
        Error? failure = null;

        RotationArguments.Read(arguments).Switch(value => retire = value, error => failure = error);

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

        FingerprintKeyRotation rotation = scope.ServiceProvider.GetRequiredService<FingerprintKeyRotation>();

        if (retire)
        {
            return (await rotation.RetireAsync(cancellationToken).ConfigureAwait(false))
                .Match(retirement => Result.Success(RotationReport.Of(retirement)), Result.Failure<string>);
        }

        return (await rotation.RecomputeAsync(cancellationToken).ConfigureAwait(false))
            .Match(progress => Result.Success(RotationReport.Of(progress)), Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the rotation itself.
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
        services.AddScoped<IKeyRotationStore>(provider => new KeyRotationStore(
            provider.GetRequiredService<StoreContext>(),
            provider.GetRequiredService<DataConnections>(),
            keys.Ring));
        services.AddScoped<IFingerprintRotationStore>(provider => new FingerprintRotationStore(
            provider.GetRequiredService<DataConnections>(),
            provider.GetRequiredService<DeploymentDataKeyStore>(),
            keys.Ring));
        services.AddScoped(provider => new FingerprintKeyRotation(
            provider.GetRequiredService<IKeyRotationStore>(),
            provider.GetRequiredService<IFingerprintRotationStore>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<TimeProvider>(),
            keys.Ring));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
