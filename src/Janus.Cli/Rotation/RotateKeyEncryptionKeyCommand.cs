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
/// The <c>rotate-kek</c> command: re-wraps every row of the subject-key table, the only
/// values held under the key-encryption key, under its current version and prints that
/// version's escrow copy, or, given <c>--sealed</c> once the copy is sealed, retires the
/// versions before it and says until when they are kept.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003, DR-009, DR-009a and OPS-SEC-001 (D-166, 316, 317). The
/// operator puts the new version in the secrets manager as current, keeping the previous
/// one, and pipes the document to the command under the maintenance credential. The
/// command needs nothing of the application, and a run that stops is resumed by running
/// it again.
/// </remarks>
internal static class RotateKeyEncryptionKeyCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "rotate-kek";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name.</param>
    /// <param name="terminal">Where the keys are read from and the escrow copy written to.</param>
    /// <param name="cancellationToken">
    /// Abandons the command; the batch in hand rolls back and the next run resumes after
    /// the last one that committed.
    /// </param>
    /// <returns>
    /// The rotation's version and the count of subject keys it re-wrapped, with the
    /// versions retired and the date they are kept until where the seal was confirmed;
    /// or the failure naming what was refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        RotationRequest request = default!;
        Error? failure = null;

        RotationArguments.Read(arguments).Switch(value => request = value, error => failure = error);

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

        KeyRotation rotation = scope.ServiceProvider.GetRequiredService<KeyRotation>();

        if (request.Sealed)
        {
            return (await rotation.RetireAsync(cancellationToken).ConfigureAwait(false))
                .Match(retirement => Result.Success(RotationReport.Of(retirement, request.Retention)), Result.Failure<string>);
        }

        Result<KeyRotationProgress> outcome = await rotation.ReWrapAsync(cancellationToken).ConfigureAwait(false);

        if (outcome.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<string>(refused);
        }

        // DR-009, OPS-SEC-003 AC4: the escrow copy of the version every value is now
        // under, for the envelope, before the report the seal is confirmed against.
        await EscrowCopy.WriteKeyEncryptionKeyAsync(terminal.Output, keys.Ring, cancellationToken).ConfigureAwait(false);

        return outcome.Match(progress => Result.Success(RotationReport.Of(progress)), Result.Failure<string>);
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
        services.AddSingleton(keys.Ring);
        services.AddStorageArea(keys.Connection);
        services.AddScoped<IKeyRotationStore>(provider => new KeyRotationStore(
            provider.GetRequiredService<StoreContext>(),
            provider.GetRequiredService<DataConnections>(),
            keys.Ring));
        services.AddScoped(provider => new KeyRotation(
            provider.GetRequiredService<IKeyRotationStore>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<TimeProvider>(),
            keys.Ring));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
