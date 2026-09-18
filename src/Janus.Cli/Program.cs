using System;
using System.Collections.Generic;

namespace Janus.Cli;

/// <summary>
/// Entry point of the command-line application of CONV-LAYOUT-001.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The exit code of an invocation that names no command this application carries.
    /// </summary>
    private const int UnknownCommandExitCode = 1;

    private static readonly Dictionary<string, Func<string[], int>> Commands = new(StringComparer.Ordinal);

    private static int Main(string[] args)
    {
        if (args.Length > 0 && Commands.TryGetValue(args[0], out Func<string[], int>? command))
        {
            return command(args);
        }

        return UnknownCommandExitCode;
    }
}
