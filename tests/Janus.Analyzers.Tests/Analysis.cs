using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// Runs one analyser over a case compiled in memory and reports what it found.
/// </summary>
internal static class Analysis
{
    private static readonly ImmutableArray<MetadataReference> References = Platform();

    // CONV-SETUP-004: the one file a rule's severity is configured in.
    private static readonly string Configuration = Path.Combine(Repository.Root, ".editorconfig");

    // CONV-CODE-008 AC2: the folders of the projects that run the analysers, which are
    // every project of the build but the analysers' own.
    private static readonly string[] Analysed = ["src", "tools"];

    /// <summary>
    /// The identifiers the analyser reported over the case, in the order they appear.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyser to run.</typeparam>
    /// <param name="source">The case.</param>
    /// <returns>The reported identifiers.</returns>
    internal static async Task<string[]> OfAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new() =>
        [.. (await ReportedAsync<TAnalyzer>(source)).Select(diagnostic => diagnostic.Id)];

    /// <summary>
    /// What the analyser reported over the case, each with the severity the build of
    /// the library gives it: the least it is set to in any source file the analysers
    /// run over, read through the compiler's own reading of the repository's
    /// .editorconfig. A rule's own setting comes first, then its category's, then the
    /// setting for every rule, then the rule's default.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyser to run.</typeparam>
    /// <param name="source">The case.</param>
    /// <returns>The reported identifiers, each with its severity in the build.</returns>
    internal static async Task<(string Id, ReportDiagnostic Severity)[]> AsBuiltAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        ImmutableArray<Diagnostic> reported = await ReportedAsync<TAnalyzer>(source);

        string text = await File.ReadAllTextAsync(Configuration, TestContext.Current.CancellationToken);

        var configuration = AnalyzerConfigSet.Create(ImmutableArray.Create(
            AnalyzerConfig.Parse(SourceText.From(text), Configuration)));

        AnalyzerConfigOptionsResult[] files =
        [
            .. Sources().Select(configuration.GetOptionsForSourcePath),
        ];

        if (files.Length == 0)
        {
            throw new InvalidOperationException("No source file of the library was found to read the configuration for.");
        }

        return
        [
            .. reported.Select(diagnostic => (
                diagnostic.Id,
                files
                    .Select(file => Severity(diagnostic.Descriptor, file))
                    .OrderBy(Leniency)
                    .Last())),
        ];
    }

    private static async Task<ImmutableArray<Diagnostic>> ReportedAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var compilation = CSharpCompilation.Create(
            "Case",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var refused = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!refused.IsEmpty)
        {
            throw new InvalidOperationException("The case does not compile: " + refused[0]);
        }

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    // The severity one file of the build gives the rule.
    private static ReportDiagnostic Severity(DiagnosticDescriptor rule, AnalyzerConfigOptionsResult file)
    {
        if (file.TreeOptions.TryGetValue(rule.Id, out ReportDiagnostic own) && own != ReportDiagnostic.Default)
        {
            return own;
        }

        if (Set(file, "dotnet_analyzer_diagnostic.category-" + rule.Category + ".severity") is ReportDiagnostic category)
        {
            return category;
        }

        if (Set(file, "dotnet_analyzer_diagnostic.severity") is ReportDiagnostic every)
        {
            return every;
        }

        return !rule.IsEnabledByDefault
            ? ReportDiagnostic.Suppress
            : rule.DefaultSeverity switch
            {
                DiagnosticSeverity.Error => ReportDiagnostic.Error,
                DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
                DiagnosticSeverity.Info => ReportDiagnostic.Info,
                _ => ReportDiagnostic.Hidden,
            };
    }

    // A severity set for many rules at once, as the file spells it.
    private static ReportDiagnostic? Set(AnalyzerConfigOptionsResult file, string key)
    {
        string? value = file.AnalyzerOptions
            .Where(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(option => option.Value)
            .FirstOrDefault();

        return value?.ToUpperInvariant() switch
        {
            null or "DEFAULT" => null,
            "ERROR" => ReportDiagnostic.Error,
            "WARNING" => ReportDiagnostic.Warn,
            "SUGGESTION" => ReportDiagnostic.Info,
            "SILENT" => ReportDiagnostic.Hidden,
            _ => ReportDiagnostic.Suppress,
        };
    }

    // How far a severity is from failing the build, an error being the least far.
    private static int Leniency(ReportDiagnostic severity) => severity switch
    {
        ReportDiagnostic.Error => 0,
        ReportDiagnostic.Warn => 1,
        ReportDiagnostic.Info => 2,
        ReportDiagnostic.Hidden => 3,
        _ => 4,
    };

    // Every source file of a project that runs the analysers.
    private static IEnumerable<string> Sources() =>
        Analysed
            .SelectMany(folder => Directory.EnumerateFiles(
                Path.Combine(Repository.Root, folder),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(file => !Path.GetRelativePath(Repository.Root, file)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or "Janus.Analyzers"));

    /// <summary>
    /// Every assembly the test host loaded, which carries the base class library, the
    /// logging abstractions and <c>Janus.Core</c>, so a case compiles against the same
    /// types the library does.
    /// </summary>
    private static ImmutableArray<MetadataReference> Platform()
    {
        string assemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The test host reported no platform assemblies.");

        IEnumerable<string> paths = assemblies
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        return [.. paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}
