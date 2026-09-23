using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// Where the product name may appear in the source: namespaces, project and package
/// identifiers and the one registration entry point (CONV-NAME-001, D-163).
/// </summary>
[Trait("kind", "contract")]
public sealed class ProductNameTests
{
    // The root namespace is the product name, so the scan reads it from there; spelling
    // it here would be the one occurrence the scan exists to refuse.
    private static readonly string Name = typeof(Result).Namespace!.Split('.')[0];

    // Every folder the build, the tests and the pipeline read.
    private static readonly string[] Roots = ["src", "tests", "tools", ".github", ".config"];

    // The documents at the root, the changelog and the notice, are written for a
    // reader of the package; the scan is of the source.
    private static readonly string[] Documents = ["*.md", "NOTICE"];

    // The first segment of a dotted name (a namespace, or a project, assembly, package
    // or solution identifier), and the entry point.
    private static readonly Regex Permitted = new(
        $@"(?<!\w)(?:{Regex.Escape(Name)}(?=\.)|Add{Regex.Escape(Name)}\b)",
        RegexOptions.None,
        TimeSpan.FromSeconds(5));

    // NuGet writes a package identifier in lower case in the lock file.
    private static readonly Regex LockedPackage = new(
        $@"""{Regex.Escape(Name)}\.",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// CONV-NAME-001 AC2: a source scan finds the product name only in namespaces,
    /// project and package identifiers and the entry point.
    /// </summary>
    [Fact]
    public void CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint()
    {
        string[] carrying = Sources()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => Carries(entry.file, entry.line))
            .Select(entry => Path.GetRelativePath(Repository.Root, entry.file) + ":" + entry.number)
            .ToArray();

        Assert.Empty(carrying);
    }

    private static bool Carries(string file, string line)
    {
        string remaining = Permitted.Replace(line, string.Empty);

        if (string.Equals(Path.GetFileName(file), "packages.lock.json", StringComparison.Ordinal))
        {
            remaining = LockedPackage.Replace(remaining, string.Empty);
        }

        return remaining.Contains(Name, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Sources() =>
        Roots
            .SelectMany(root =>
                Directory.EnumerateFiles(Path.Combine(Repository.Root, root), "*", SearchOption.AllDirectories))
            .Where(file => !Generated(file) && !Vendored(file))
            .Concat(Directory
                .EnumerateFiles(Repository.Root)
                .Where(file => !Documents.Any(document =>
                    FileSystemName.MatchesSimpleExpression(document, Path.GetFileName(file)))));

    private static bool Generated(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    // The Unicode Character Database files, vendored as Unicode publishes them.
    private static bool Vendored(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "ucd" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
