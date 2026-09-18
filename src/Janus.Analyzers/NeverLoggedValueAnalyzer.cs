using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janus.Analyzers;

/// <summary>
/// JAN0002: reports a logging call that takes a value marked as never logged.
/// </summary>
/// <remarks>Implements CONV-CODE-008, rule JAN0002, serving CONV-LOG-003.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NeverLoggedValueAnalyzer : DiagnosticAnalyzer
{
    private const string NeverLoggedAttributeName = "Janus.Core.NeverLoggedAttribute";
    private const string LoggerMessageAttributeName = "Microsoft.Extensions.Logging.LoggerMessageAttribute";
    private const string LoggerInterfaceName = "Microsoft.Extensions.Logging.ILogger";
    private const string LoggerExtensionsName = "Microsoft.Extensions.Logging.LoggerExtensions";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.NeverLoggedValue);

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
        INamedTypeSymbol? neverLogged = context.Compilation.GetTypeByMetadataName(NeverLoggedAttributeName);

        if (neverLogged is null)
        {
            return;
        }

        var logging = new LoggingCallShape(
            context.Compilation.GetTypeByMetadataName(LoggerMessageAttributeName),
            context.Compilation.GetTypeByMetadataName(LoggerInterfaceName),
            context.Compilation.GetTypeByMetadataName(LoggerExtensionsName));

        context.RegisterSyntaxNodeAction(
            node => Analyze(node, neverLogged, logging),
            SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol neverLogged, LoggingCallShape logging)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!logging.IsLoggingCall(method))
        {
            return;
        }

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            string? offender = Offender(context, argument.Expression, neverLogged);

            if (offender is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.NeverLoggedValue,
                    argument.GetLocation(),
                    offender));
            }
        }
    }

    /// <summary>
    /// The name of the marked type or member the argument carries, or null when it
    /// carries none.
    /// </summary>
    private static string? Offender(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, INamedTypeSymbol neverLogged)
    {
        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;

        if (symbol is not null && HasAttribute(symbol, neverLogged))
        {
            return symbol.Name;
        }

        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (HasAttribute(current, neverLogged))
            {
                return current.Name;
            }
        }

        return null;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute)
    {
        return symbol.GetAttributes()
            .Any(data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute));
    }
}
