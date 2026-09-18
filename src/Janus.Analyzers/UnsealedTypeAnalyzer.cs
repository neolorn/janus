using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0003: reports a public or internal non-abstract class that is not sealed.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0003, serving CONV-CODE-001.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UnsealedTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.UnsealedType);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsSealed || type.IsStatic)
        {
            return;
        }

        if (type.IsImplicitlyDeclared)
        {
            return;
        }

        if (type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return;
        }

        foreach (Location location in type.Locations)
        {
            if (location.IsInSource)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.UnsealedType, location, type.Name));
            }
        }
    }
}
