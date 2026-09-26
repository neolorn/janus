using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
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
/// The <c>rotate-kek</c> command: re-wraps every value held under the key-encryption key
/// under its current version and prints that version's escrow copy, or, given
/// <c>--sealed</c> once the copy is sealed, retires the versions before it.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003, DR-009, DR-009a and OPS-SEC-001, as entries 316 and 317 of the
/// decisions pending review settle them. The operator puts the new version in the
/// secrets manager as current, keeping the previous one, and pipes the document to the
/// command under the maintenance credential. The command needs nothing of the
/// application, and a run that stops is resumed by running it again.
/// </remarks>
internal static class RotateKeyEncryptionKeyCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "rotate-kek";

    /// <summary>
    /// The argument that confirms the escrow copy sealed.
    /// </summary>
    public const string Sealed = "--sealed";

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
    /// versions retired where the seal was confirmed; or the failure naming what was
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

        if (arguments.Count > 1 || (arguments.Count == 1 && arguments[0] != Sealed))
        {
            return Result.Failure<string>(
                Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(arguments[^1])));
        }

        using var held = new HeldKeys();
        Error? failure = null;

        KeyDocument keys = (await KeyDocument.ReadAsync(terminal, held, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<KeyDocument>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        await using ServiceProvider services = Composed(keys);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        KeyRotation rotation = scope.ServiceProvider.GetRequiredService<KeyRotation>();

        if (arguments.Count == 1)
        {
            return (await rotation.RetireAsync(cancellationToken).ConfigureAwait(false))
                .Match(retirement => Result.Success(Report(retirement.Rotation, retirement.Retired)), Result.Failure<string>);
        }

        Result<KeyRotationProgress> outcome = await rotation.ReWrapAsync(cancellationToken).ConfigureAwait(false);

        if (outcome.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<string>(refused);
        }

        // DR-009, OPS-SEC-003 AC4: the escrow copy of the version every value is now
        // under, for the envelope, before the report the seal is confirmed against.
        await EscrowCopy.WriteAsync(terminal.Output, keys.KeyEncryptionKeys, cancellationToken).ConfigureAwait(false);

        return outcome.Match(progress => Result.Success(Report(progress, retired: null)), Result.Failure<string>);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The version and the count, and the versions retired where there are any; nothing
    // of a key.
    private static string Report(KeyRotationProgress progress, IReadOnlyList<int>? retired)
    {
        var report = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = progress.Version,
            ["processed"] = progress.Processed,
        };

        if (retired is not null)
        {
            report["retired"] = retired;
        }

        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(report));
    }

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the rotation itself.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddStorageArea(keys.Connection, keys.KeyEncryptionKeys, keys.FingerprintKeys);
        services.AddScoped<IKeyRotationStore>(provider => new KeyRotationStore(
            provider.GetRequiredService<StoreContext>(),
            provider.GetRequiredService<DataConnections>(),
            keys.KeyEncryptionKeys));
        services.AddScoped(provider => new KeyRotation(
            provider.GetRequiredService<IKeyRotationStore>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<TimeProvider>(),
            keys.KeyEncryptionKeys));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
