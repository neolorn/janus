using System;
using System.IO;

namespace Janus.Analyzers.Tests;

/// <summary>
/// The repository the tests read when a criterion is about the build of the solution
/// rather than the behaviour of a rule alone.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// The directory holding the solution file, found by walking up from the test
    /// assembly's location.
    /// </summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Janus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root holding Janus.slnx was not found above the test assembly.");
    }
}
