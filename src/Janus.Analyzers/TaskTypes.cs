using Microsoft.CodeAnalysis;

namespace Janus.Analyzers;

/// <summary>
/// The task and awaiter types JAN0004 recognises, as the compilation under analysis
/// knows them.
/// </summary>
internal readonly struct TaskTypes
{
    private readonly INamedTypeSymbol? _task;
    private readonly INamedTypeSymbol? _genericTask;
    private readonly INamedTypeSymbol? _valueTask;
    private readonly INamedTypeSymbol? _genericValueTask;
    private readonly INamedTypeSymbol? _taskAwaiter;
    private readonly INamedTypeSymbol? _genericTaskAwaiter;
    private readonly INamedTypeSymbol? _valueTaskAwaiter;
    private readonly INamedTypeSymbol? _genericValueTaskAwaiter;

    private TaskTypes(Compilation compilation)
    {
        _task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        _genericTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        _valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        _genericValueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        _taskAwaiter = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.TaskAwaiter");
        _genericTaskAwaiter = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.TaskAwaiter`1");
        _valueTaskAwaiter = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ValueTaskAwaiter");
        _genericValueTaskAwaiter = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ValueTaskAwaiter`1");
    }

    /// <summary>
    /// Resolves the task and awaiter types against a compilation.
    /// </summary>
    internal static TaskTypes From(Compilation compilation) => new(compilation);

    /// <summary>
    /// Whether a type is a task or a value task.
    /// </summary>
    internal bool IsTask(ITypeSymbol? type) => Matches(type, _task, _genericTask, _valueTask, _genericValueTask);

    /// <summary>
    /// Whether a type is the awaiter of a task or of a value task.
    /// </summary>
    internal bool IsAwaiter(ITypeSymbol? type) =>
        Matches(type, _taskAwaiter, _genericTaskAwaiter, _valueTaskAwaiter, _genericValueTaskAwaiter);

    private static bool Matches(ITypeSymbol? type, params INamedTypeSymbol?[] candidates)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        INamedTypeSymbol definition = named.OriginalDefinition;

        foreach (INamedTypeSymbol? candidate in candidates)
        {
            if (SymbolEqualityComparer.Default.Equals(definition, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
