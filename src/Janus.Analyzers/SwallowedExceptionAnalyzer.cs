using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0006: reports a catch block that is empty, or whose every path neither throws,
/// rethrows, returns a failure result nor calls a logging method.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0006, serving CONV-ERR-003.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SwallowedExceptionAnalyzer : DiagnosticAnalyzer
{
    private const string FailureFactoryName = "Failure";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.SwallowedException);

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

        var logging = new LoggingCallShape(
            context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerMessageAttribute"),
            context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger"),
            context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions"));

        context.RegisterSyntaxNodeAction(node => Analyze(node, results, logging), SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ResultTypes results, LoggingCallShape logging)
    {
        var clause = (CatchClauseSyntax)context.Node;

        if (Handles(context, clause.Block, results, logging))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rules.SwallowedException, clause.CatchKeyword.GetLocation()));
    }

    /// <summary>
    /// Whether the block deals with the exception. A logging call deals with it
    /// wherever it stands. Otherwise every path has to leave the block, by a throw,
    /// which makes the end point unreachable and produces no exit point, or by a
    /// return of a failure result.
    /// </summary>
    private static bool Handles(
        SyntaxNodeAnalysisContext context,
        BlockSyntax block,
        ResultTypes results,
        LoggingCallShape logging)
    {
        if (block.Statements.Count == 0)
        {
            return false;
        }

        if (CallsALoggingMethod(context, block, logging))
        {
            return true;
        }

        ControlFlowAnalysis? flow = context.SemanticModel.AnalyzeControlFlow(block);

        if (flow is null || !flow.Succeeded || flow.EndPointIsReachable)
        {
            return false;
        }

        return flow.ExitPoints.All(exit => IsFailureReturn(context, results, exit));
    }

    private static bool CallsALoggingMethod(SyntaxNodeAnalysisContext context, BlockSyntax block, LoggingCallShape logging)
    {
        return Own(block)
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol)
            .OfType<IMethodSymbol>()
            .Any(logging.IsLoggingCall);
    }

    private static bool IsFailureReturn(SyntaxNodeAnalysisContext context, ResultTypes results, SyntaxNode exit)
    {
        if (exit is not ReturnStatementSyntax { Expression: { } expression })
        {
            return false;
        }

        ExpressionSyntax returned = expression is AwaitExpressionSyntax await ? await.Expression : expression;

        if (returned is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && string.Equals(method.Name, FailureFactoryName, StringComparison.Ordinal)
            && results.Contains(method.ReturnType);
    }

    /// <summary>
    /// The nodes the block itself executes, leaving out any lambda or local function
    /// declared within it.
    /// </summary>
    private static IEnumerable<SyntaxNode> Own(BlockSyntax block)
    {
        return block.DescendantNodes(
            node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
    }
}
