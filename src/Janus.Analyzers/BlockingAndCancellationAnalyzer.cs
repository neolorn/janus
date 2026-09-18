using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0004: reports blocking on a task, and an asynchronous method that takes no
/// cancellation token.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0004, serving CONV-CODE-002.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class BlockingAndCancellationAnalyzer : DiagnosticAnalyzer
{
    private const string CancellationTokenName = "System.Threading.CancellationToken";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.BlockingOnTask);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var tasks = TaskTypes.From(context.Compilation);
        INamedTypeSymbol? token = context.Compilation.GetTypeByMetadataName(CancellationTokenName);

        context.RegisterSyntaxNodeAction(node => AnalyzeMemberAccess(node, tasks), SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSymbolAction(symbol => AnalyzeMethod(symbol, token), SymbolKind.Method);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, TaskTypes tasks)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        string member = access.Name.Identifier.ValueText;

        if (member is not ("Result" or "Wait" or "GetResult"))
        {
            return;
        }

        ITypeSymbol? receiver = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;

        bool blocking = member switch
        {
            "Result" => tasks.IsTask(receiver),
            "Wait" => tasks.IsTask(receiver),
            _ => tasks.IsAwaiter(receiver),
        };

        if (blocking)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.BlockingOnTask,
                access.Name.GetLocation(),
                "Blocking on a task; await it instead"));
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol? token)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!method.IsAsync || method.MethodKind != MethodKind.Ordinary || token is null)
        {
            return;
        }

        // An override or an interface implementation carries a signature declared
        // elsewhere; the declaration is where CONV-CODE-002 applies.
        if (method.IsOverride || ImplementsAnInterfaceMember(method))
        {
            return;
        }

        if (method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, token)))
        {
            return;
        }

        foreach (Location location in method.Locations.Where(location => location.IsInSource))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.BlockingOnTask,
                location,
                FormattableString.Invariant($"'{method.Name}' is asynchronous and takes no cancellation token")));
        }
    }

    private static bool ImplementsAnInterfaceMember(IMethodSymbol method)
    {
        return method.ContainingType.AllInterfaces
            .SelectMany(contract => contract.GetMembers().OfType<IMethodSymbol>())
            .Any(member => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(member),
                method));
    }
}
