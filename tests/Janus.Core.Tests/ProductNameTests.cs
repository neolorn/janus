using System;
using System.Collections.Generic;
using System.Globalization;
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

    // The second segments a dotted name headed by the product name may carry: the name
    // of each project folder of the solution, and the solution file's extension.
    private static readonly string[] Projects =
    [
        .. new[] { "src", "tests", "tools" }
            .SelectMany(root => Directory.EnumerateDirectories(Path.Combine(Repository.Root, root)))
            .Select(folder => Path.GetFileName(folder).Split('.'))
            .Where(segments => segments.Length > 1 && string.Equals(segments[0], Name, StringComparison.Ordinal))
            .Select(segments => segments[1])
            .Append("slnx")
            .Distinct(StringComparer.Ordinal),
    ];

    // The head of a dotted name whose second segment is a project of the solution (a
    // namespace, or a project, assembly, package or solution identifier), and the entry
    // point.
    private static readonly Regex Permitted = Heads(RegexOptions.None);

    // NuGet writes a package identifier in lower case in the lock file.
    private static readonly Regex LockedPackage = Heads(RegexOptions.IgnoreCase);

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

    /// <summary>
    /// CONV-NAME-001 AC2: a dot after the name is not enough. A dotted header or string
    /// constant whose second segment names no project of the solution is found, and a
    /// namespace declaration is not.
    /// </summary>
    /// <param name="line">The line the scan reads.</param>
    /// <param name="found">Whether the scan finds the name in it.</param>
    [Theory]
    [InlineData("context.Response.Headers[\"{0}.Correlation\"] = value;", true)]
    [InlineData("private const string Key = \"{0}.Setting\";", true)]
    [InlineData("namespace {0}.Hosting.Bff;", false)]
    [InlineData("services.Add{0}(declaration);", false)]
    public void CONV_NAME_001_AC2_ADottedNameThatIsNoProjectIsFound(string line, bool found) =>
        Assert.Equal(found, Carries("Source.cs", string.Format(CultureInfo.InvariantCulture, line, Name)));

    /// <summary>
    /// CONV-NAME-001 AC2: in the lock file the package identifiers are held to the same
    /// rule in lower case, and nothing else is let through.
    /// </summary>
    /// <param name="line">The line the scan reads.</param>
    /// <param name="found">Whether the scan finds the name in it.</param>
    [Theory]
    [InlineData("\"{0}.core\": {{", false)]
    [InlineData("\"{0}.setting\": {{", true)]
    public void CONV_NAME_001_AC2_ALockedPackageIsHeldToTheProjectsInLowerCase(string line, bool found) =>
        Assert.Equal(
            found,
            Carries(
                "packages.lock.json",
                string.Format(CultureInfo.InvariantCulture, line, CultureInfo.InvariantCulture.TextInfo.ToLower(Name))));

    private static bool Carries(string file, string line)
    {
        string remaining = Permitted.Replace(line, string.Empty);

        if (string.Equals(Path.GetFileName(file), "packages.lock.json", StringComparison.Ordinal))
        {
            remaining = LockedPackage.Replace(remaining, string.Empty);
        }

        return remaining.Contains(Name, StringComparison.OrdinalIgnoreCase);
    }

    private static Regex Heads(RegexOptions options) =>
        new(
            $@"(?<!\w)(?:{Regex.Escape(Name)}(?=\.(?:{string.Join('|', Projects.Select(Regex.Escape))})\b)|Add{Regex.Escape(Name)}\b)",
            options,
            TimeSpan.FromSeconds(5));

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
