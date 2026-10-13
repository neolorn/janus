using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A logger holding what was recorded so a test can read it back.
/// </summary>
/// <typeparam name="TCategory">What the entries are recorded under.</typeparam>
internal sealed class LogInMemory<TCategory> : ILogger<TCategory>
{
    private readonly List<(LogLevel Level, int EventId)> _entries = [];

    private readonly List<IReadOnlyDictionary<string, string?>> _carried = [];

    /// <summary>
    /// Every entry recorded, in order.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, int EventId)> Entries => _entries;

    /// <summary>
    /// What each entry carried, in the same order: every value by the name the entry
    /// gives it, as text.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string?>> Carried => _carried;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add((logLevel, eventId.Id));
        _carried.Add(state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToDictionary(
                value => value.Key,
                value => Convert.ToString(value.Value, CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal));
    }
}
