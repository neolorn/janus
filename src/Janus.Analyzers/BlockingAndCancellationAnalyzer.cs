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

    private const string UnitOfWorkName = "Janus.Core.IUnitOfWork";

    private const string RollbackName = "RollbackAsync";

    // The first part of the name of every assembly whose interfaces are the library's
    // own contracts, which is the first part of this one's namespace.
    private static readonly string Library = typeof(BlockingAndCancellationAnalyzer).Namespace.Split('.')[0];

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

        if (method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, token)))
        {
            return;
        }

        // An override carries a signature declared elsewhere. So does a member that
        // implements an interface, and CONV-CODE-002 exempts it where the interface is
        // not the library's own, and where it is the rollback of the unit of work.
        if (method.IsOverride || ImplementsAnExemptMember(method))
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

    private static bool ImplementsAnExemptMember(IMethodSymbol method)
    {
        return method.ContainingType.AllInterfaces
            .SelectMany(contract => contract.GetMembers().OfType<IMethodSymbol>())
            .Any(member => IsExempt(member, method.ContainingAssembly)
                && SymbolEqualityComparer.Default.Equals(
                    method.ContainingType.FindImplementationForInterfaceMember(member),
                    method));
    }

    private static bool IsExempt(IMethodSymbol member, IAssemblySymbol implementing)
    {
        if (member.Name == RollbackName && member.ContainingType.ToDisplayString() == UnitOfWorkName)
        {
            return true;
        }

        IAssemblySymbol declaring = member.ContainingAssembly;

        return !SymbolEqualityComparer.Default.Equals(declaring, implementing)
            && declaring.Name != Library
            && !declaring.Name.StartsWith(Library + ".", StringComparison.Ordinal);
    }
}
