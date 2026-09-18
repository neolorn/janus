using Microsoft.CodeAnalysis;

namespace Janus.Analyzers;

/// <summary>
/// The result types of CONV-DESIGN-005 as the compilation under analysis knows them.
/// </summary>
internal readonly struct ResultTypes
{
    private const string ResultMetadataName = "Janus.Core.Result";
    private const string GenericResultMetadataName = "Janus.Core.Result`1";

    private readonly INamedTypeSymbol? _result;
    private readonly INamedTypeSymbol? _genericResult;

    private ResultTypes(INamedTypeSymbol? result, INamedTypeSymbol? genericResult)
    {
        _result = result;
        _genericResult = genericResult;
    }

    /// <summary>
    /// Resolves the result types against a compilation. Either may be absent, in which
    /// case no type matches it.
    /// </summary>
    internal static ResultTypes From(Compilation compilation)
    {
        return new ResultTypes(
            compilation.GetTypeByMetadataName(ResultMetadataName),
            compilation.GetTypeByMetadataName(GenericResultMetadataName));
    }

    /// <summary>
    /// Whether a type is <c>Result</c> or a construction of <c>Result&lt;T&gt;</c>.
    /// </summary>
    internal bool Contains(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        INamedTypeSymbol definition = named.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(definition, _result)
            || SymbolEqualityComparer.Default.Equals(definition, _genericResult);
    }
}
