using System;
using System.IO;

namespace Janus.UnicodeTables;

/// <summary>
/// Writes the Unicode tables that <c>Janus.Core</c> carries, from the Unicode Character
/// Database vendored beside this project.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and CONV-LAYOUT-001. The tables are checked in and the gate
/// of CONV-GATE-001 runs this again and fails on a diff, so the canonical form the
/// library computes is the pinned version's and no machine's installed library can
/// change it.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// The version the vendored files are read at and every table is generated from.
    /// </summary>
    internal const string Version = "17.0.0";

    private static int Main(string[] arguments)
    {
        string root = RepositoryRoot();
        string database = arguments is { Length: > 0 }
            ? arguments[0]
            : Path.Combine(root, "tools", "Janus.UnicodeTables", "ucd");
        string output = arguments is { Length: > 1 }
            ? arguments[1]
            : Path.Combine(root, "src", "Janus.Core", "Unicode");

        var files = new DatabaseFiles(database, Version);
        var characters = UnicodeCharacterDatabase.Read(files);

        Directory.CreateDirectory(output);
        TableEmitter.Emit(characters, output, Version);

        Console.Out.WriteLine("Unicode " + Version + " tables written to " + output);

        return 0;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Janus.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root is not above the running assembly.");
    }
}
