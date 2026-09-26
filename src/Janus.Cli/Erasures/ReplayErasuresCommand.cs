using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Cli.Erasures;

/// <summary>
/// The <c>replay-erasures</c> command: reads a copy of the off-host erasure ledger and
/// carries out again every erasure it records that the restored database does not.
/// </summary>
/// <remarks>
/// Implements DR-016 AC3 and DR-006a AC1, as entry 333 of the decisions pending review
/// settles them. The operator copies the ledger from its storage, names the copy, and
/// pipes the key document as for every command. Every line is read before anything else
/// is, so a ledger holding one line in another form is refused whole, naming the line,
/// and nothing is written. Running the command again over the same ledger changes
/// nothing more.
/// </remarks>
internal static class ReplayErasuresCommand
{
    /// <summary>
    /// The command's name on the command line.
    /// </summary>
    public const string Name = "replay-erasures";

    private const string Ledger = "ledger";

    // DR-016: the ledger is UTF-8 with no header, so bytes that are not UTF-8 are refused
    // rather than read as a replacement character, and no byte order mark is taken as
    // naming another encoding.
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The arguments that follow the command's name: the ledger's path.</param>
    /// <param name="terminal">Where the keys are read from.</param>
    /// <param name="cancellationToken">
    /// Abandons the command; the erasure in hand rolls back and the next run carries on
    /// from what committed.
    /// </param>
    /// <returns>
    /// How many lines were carried out again, stood already, or named no account; or the
    /// failure naming what was refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<Result<string>> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        if (arguments.Count != 1)
        {
            return Result.Failure<string>(Malformed());
        }

        Error? failure = null;

        IReadOnlyList<ErasureLedgerLine> lines = (await LinesAsync(arguments[0], cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<ErasureLedgerLine>>(error, ref failure));

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

        ReplayedErasures replayed = await scope.ServiceProvider
            .GetRequiredService<ErasureReplay>()
            .ReplayAsync(lines, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Report(replayed));
    }

    // DR-016 AC3: the whole ledger, every line in the one form it is written in. A file
    // that cannot be opened or decoded is refused as the ledger, and a line that is not
    // in that form is refused by its number, which carries nothing of what it held.
    private static async Task<Result<IReadOnlyList<ErasureLedgerLine>>> LinesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var written = new List<string>();

        try
        {
            using var reader = new StreamReader(path, Strict, detectEncodingFromByteOrderMarks: false);

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
            {
                written.Add(line);
            }
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Result.Failure<IReadOnlyList<ErasureLedgerLine>>(Malformed());
        }

        var lines = new List<ErasureLedgerLine>(written.Count);

        for (int number = 1; number <= written.Count; number++)
        {
            if (ErasureLedgerLine.Read(written[number - 1]) is not ErasureLedgerLine line)
            {
                return Result.Failure<IReadOnlyList<ErasureLedgerLine>>(new Error(
                    ErrorCodes.RequestMalformed,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["member"] = JsonSerializer.SerializeToElement(Ledger),
                        ["line"] = JsonSerializer.SerializeToElement(number),
                    }));
            }

            lines.Add(line);
        }

        return Result.Success<IReadOnlyList<ErasureLedgerLine>>(lines);
    }

    private static Error Malformed() =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(Ledger));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The three counts, and nothing of a subject.
    private static string Report(ReplayedErasures replayed) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["reapplied"] = replayed.Reapplied,
                ["standing"] = replayed.Standing,
                ["absent"] = replayed.Absent,
            }));

    // What the command runs over: the storage area under the connection and the keys
    // the document carried, and the replay itself.
    private static ServiceProvider Composed(KeyDocument keys)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddStorageArea(keys.Connection, keys.KeyEncryptionKeys, keys.FingerprintKeys);
        services.AddScoped<ErasureReplay>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
