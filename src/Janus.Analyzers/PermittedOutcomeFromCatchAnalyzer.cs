using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0001: reports a catch block that returns a permitted, authenticated or
/// successful outcome.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0001, serving CONV-ERR-002.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class PermittedOutcomeFromCatchAnalyzer : DiagnosticAnalyzer
{
    private const string SuccessFactoryName = "Success";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.PermittedOutcomeFromCatch);

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
        var results = ResultTypes.From(context.Compilation);
        context.RegisterSyntaxNodeAction(node => Analyze(node, results), SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ResultTypes results)
    {
        var clause = (CatchClauseSyntax)context.Node;

        foreach (ReturnStatementSyntax statement in ReturnsOwnedBy(clause))
        {
            ExpressionSyntax? expression = statement.Expression;

            if (expression is null)
            {
                continue;
            }

            if (expression.IsKind(SyntaxKind.TrueLiteralExpression) || IsSuccessFactory(context, results, expression))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.PermittedOutcomeFromCatch, statement.GetLocation()));
            }
        }
    }

    /// <summary>
    /// The return statements the catch block itself executes. A return inside a lambda
    /// or a local function declared within the block belongs to that function, not to
    /// the failure path.
    /// </summary>
    private static ImmutableArray<ReturnStatementSyntax> ReturnsOwnedBy(CatchClauseSyntax clause)
    {
        return clause.Block
            .DescendantNodes(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>()
            .ToImmutableArray();
    }

    private static bool IsSuccessFactory(SyntaxNodeAnalysisContext context, ResultTypes results, ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        return string.Equals(method.Name, SuccessFactoryName, StringComparison.Ordinal)
            && results.Contains(method.ContainingType);
    }
}
