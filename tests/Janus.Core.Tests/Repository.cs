using System;
using System.IO;

namespace Janus.Core.Tests;

/// <summary>
/// The repository the tests read when a criterion is about the shape of the solution
/// rather than the behaviour of a type.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// The directory holding the solution file, found by walking up from the test
    /// assembly's location.
    /// </summary>
    public static string Root { get; } = Find();

    /// <summary>
    /// Reads a file given by its path relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The path relative to the root.</param>
    /// <returns>The file's text.</returns>
    public static string ReadText(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

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
