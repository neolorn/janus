using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A logger holding what was recorded so a test can read it back.
/// </summary>
/// <typeparam name="TCategory">What the entries are recorded under.</typeparam>
internal sealed class LogInMemory<TCategory> : ILogger<TCategory>
{
    private readonly List<(LogLevel Level, int EventId)> _entries = [];

    /// <summary>
    /// Every entry recorded, in order.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, int EventId)> Entries => _entries;

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
        Func<TState, Exception?, string> formatter) =>
        _entries.Add((logLevel, eventId.Id));
}
