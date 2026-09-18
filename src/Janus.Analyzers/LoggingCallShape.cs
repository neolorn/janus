using System.Linq;
using Microsoft.CodeAnalysis;

namespace Janus.Analyzers;

/// <summary>
/// Recognises the logging calls of CONV-LOG-001: a method the logging source
/// generator produced, and the logger interface and its extensions.
/// </summary>
internal sealed class LoggingCallShape
{
    private readonly INamedTypeSymbol? _loggerMessageAttribute;
    private readonly INamedTypeSymbol? _loggerInterface;
    private readonly INamedTypeSymbol? _loggerExtensions;

    internal LoggingCallShape(
        INamedTypeSymbol? loggerMessageAttribute,
        INamedTypeSymbol? loggerInterface,
        INamedTypeSymbol? loggerExtensions)
    {
        _loggerMessageAttribute = loggerMessageAttribute;
        _loggerInterface = loggerInterface;
        _loggerExtensions = loggerExtensions;
    }

    /// <summary>
    /// Whether an invoked method writes to a log.
    /// </summary>
    internal bool IsLoggingCall(IMethodSymbol method)
    {
        if (_loggerMessageAttribute is not null
            && method.GetAttributes().Any(data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, _loggerMessageAttribute)))
        {
            return true;
        }

        INamedTypeSymbol containing = method.ContainingType;

        return SymbolEqualityComparer.Default.Equals(containing, _loggerExtensions)
            || SymbolEqualityComparer.Default.Equals(containing, _loggerInterface)
            || containing.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, _loggerInterface));
    }
}
