using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0005: reports an expression of result type whose value is discarded.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0005, serving CONV-DESIGN-005.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class DiscardedResultAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.DiscardedResult);

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
        context.RegisterSyntaxNodeAction(node => Analyze(node, results), SyntaxKind.ExpressionStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ResultTypes results)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        ExpressionSyntax discarded = Discarded(statement.Expression);
        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(discarded, context.CancellationToken).Type;

        if (results.Contains(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.DiscardedResult, discarded.GetLocation()));
        }
    }

    /// <summary>
    /// The expression whose value the statement throws away: the right-hand side of an
    /// assignment to a discard, and otherwise the statement's own expression.
    /// </summary>
    private static ExpressionSyntax Discarded(ExpressionSyntax expression)
    {
        if (expression is AssignmentExpressionSyntax assignment
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "_" })
        {
            return assignment.Right;
        }

        return expression;
    }
}
