using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Cli.Bootstrap;
using Janus.Cli.Configuration;
using Janus.Cli.Rotation;
using Janus.Core;

namespace Janus.Cli;

/// <summary>
/// Entry point of the command-line application of CONV-LAYOUT-001.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001, API-CONV-002 and CONV-CONTENT-001. What a command produces
/// goes to standard output; a refusal goes to standard error as the code and the
/// structured data the API carries, never as a sentence.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// The exit code of a command that did what it was asked.
    /// </summary>
    private const int SucceededExitCode = 0;

    /// <summary>
    /// The exit code of a command that refused.
    /// </summary>
    private const int RefusedExitCode = 1;

    /// <summary>
    /// The exit code of an invocation that names no command this application carries.
    /// </summary>
    private const int UnknownCommandExitCode = 1;

    private static readonly Dictionary<string, Func<IReadOnlyList<string>, Terminal, CancellationToken, Task<Result<string>>>> Commands =
        new(StringComparer.Ordinal)
        {
            [BootstrapCommand.Name] = BootstrapCommand.RunAsync,
            [RotateKeyEncryptionKeyCommand.Name] = RotateKeyEncryptionKeyCommand.RunAsync,
            [RotateFingerprintKeyCommand.Name] = RotateFingerprintKeyCommand.RunAsync,
            [ConfigureCommand.Name] = ConfigureCommand.RunAsync,
        };

    /// <summary>
    /// Runs the command the arguments name.
    /// </summary>
    /// <param name="arguments">The command's name, then its arguments.</param>
    /// <param name="terminal">Where the command reads and writes.</param>
    /// <param name="cancellationToken">Abandons the command.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Terminal terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(terminal);

        if (arguments.Count is 0
            || !Commands.TryGetValue(arguments[0], out Func<IReadOnlyList<string>, Terminal, CancellationToken, Task<Result<string>>>? command))
        {
            return UnknownCommandExitCode;
        }

        Result<string> outcome = await command([.. arguments.Skip(1)], terminal, cancellationToken).ConfigureAwait(false);

        return await outcome
            .Match(
                async produced =>
                {
                    await terminal.Output.WriteLineAsync(produced).ConfigureAwait(false);

                    return SucceededExitCode;
                },
                async refused =>
                {
                    await terminal.Error.WriteLineAsync(Written(refused)).ConfigureAwait(false);

                    return RefusedExitCode;
                })
            .ConfigureAwait(false);
    }

    // The runtime fixes the entry point's signature, so the token starts here.
    private static Task<int> Main(string[] args) => RunOnProcessAsync(args, CancellationToken.None);

    private static async Task<int> RunOnProcessAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        await using Stream input = Console.OpenStandardInput();

        return await RunAsync(
                arguments,
                new Terminal(input, Console.IsInputRedirected, Console.Out, Console.Error),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // API-CONV-002: the code and its structured data, in the shape an API refusal
    // carries them.
    private static string Written(Error refused)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("code", refused.Code.ToString());
            json.WriteStartObject("details");

            foreach ((string name, JsonElement value) in refused.Details)
            {
                json.WritePropertyName(name);
                value.WriteTo(json);
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
