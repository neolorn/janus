using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers.Tests;

/// <summary>
/// Runs one analyser over a case compiled in memory and reports what it found.
/// </summary>
internal static class Analysis
{
    private static readonly ImmutableArray<MetadataReference> References = Platform();

    /// <summary>
    /// The identifiers the analyser reported over the case, in the order they appear.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyser to run.</typeparam>
    /// <param name="source">The case.</param>
    /// <returns>The reported identifiers.</returns>
    internal static async Task<string[]> OfAsync<TAnalyzer>(string source)
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

        ImmutableArray<Diagnostic> reported = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return [.. reported.Select(diagnostic => diagnostic.Id)];
    }

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
