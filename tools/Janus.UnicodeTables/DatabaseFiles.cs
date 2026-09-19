using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Janus.UnicodeTables;

/// <summary>
/// The vendored Unicode Character Database, read at one pinned version.
/// </summary>
/// <remarks>
/// Every file but <c>UnicodeData.txt</c> carries its version in its first line. Reading
/// it back and refusing a mismatch is what makes the pin real: a file replaced by hand
/// at another version fails here rather than silently changing a canonical form.
/// <c>UnicodeData.txt</c> carries no version line, so its pin rests on the vendored copy
/// and on the gate that regenerates the tables and fails on a diff.
/// </remarks>
internal sealed class DatabaseFiles
{
    private const string Missing = "# @missing:";

    private readonly string _directory;
    private readonly string _version;

    /// <summary>
    /// The database in a directory, at a version.
    /// </summary>
    /// <param name="directory">The directory the files are vendored in.</param>
    /// <param name="version">The version every file that names one must carry.</param>
    internal DatabaseFiles(string directory, string version)
    {
        _directory = directory;
        _version = version;
    }

    /// <summary>
    /// Reads a file's data lines: comments and blank lines removed, each remaining line
    /// split on semicolons and trimmed.
    /// </summary>
    /// <param name="name">The file's name, relative to the directory.</param>
    /// <returns>One array of fields per data line.</returns>
    internal IEnumerable<string[]> Fields(string name)
    {
        foreach (string line in Lines(name))
        {
            int comment = line.IndexOf('#', StringComparison.Ordinal);
            string data = comment < 0 ? line : line[..comment];

            if (data.Trim().Length == 0)
            {
                continue;
            }

            string[] fields = data.Split(';');

            for (int index = 0; index < fields.Length; index++)
            {
                fields[index] = fields[index].Trim();
            }

            yield return fields;
        }
    }

    /// <summary>
    /// Reads a file's <c>@missing</c> lines, which give the value of the code points the
    /// file lists no line for.
    /// </summary>
    /// <param name="name">The file's name, relative to the directory.</param>
    /// <returns>The range and value of each default, in the order the file gives them.</returns>
    internal IEnumerable<(int First, int Last, string Value)> Defaults(string name)
    {
        foreach (string line in Lines(name))
        {
            if (!line.StartsWith(Missing, StringComparison.Ordinal))
            {
                continue;
            }

            string[] fields = line[Missing.Length..].Split(';');

            if (fields.Length < 2)
            {
                continue;
            }

            (int first, int last) = CodePoints.Range(fields[0].Trim());

            yield return (first, last, fields[1].Trim());
        }
    }

    private IEnumerable<string> Lines(string name)
    {
        string path = Path.Combine(_directory, name.Replace('/', Path.DirectorySeparatorChar));
        string expected = Path.GetFileNameWithoutExtension(name) + "-" + _version + ".txt";
        bool first = true;

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.TrimEnd('\r');

            if (first)
            {
                first = false;

                if (line.StartsWith('#') && !line.Contains(expected, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{path} is not {expected}; the vendored database is at another version."));
                }
            }

            yield return line;
        }
    }
}
