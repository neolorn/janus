using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests;

/// <summary>
/// Every entry a deployment writes through one logging provider, with its category,
/// its level and the values of the scopes open around it, so a test can say what an
/// entry was written under (CONV-LOG-002).
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class EntriesInMemory : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<Entry> _written = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    /// <summary>
    /// Every entry written, in order.
    /// </summary>
    public IReadOnlyCollection<Entry> Written => _written;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Writer(categoryName, this);

    /// <inheritdoc/>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    /// <summary>
    /// Opens the scope the web server opens around a request, carrying its
    /// <c>RequestId</c>, for a deployment that runs its pipeline without a server
    /// (CONV-LOG-002).
    /// </summary>
    /// <param name="requestId">The request's identifier.</param>
    /// <returns>What closes the scope.</returns>
    public IDisposable? Request(string requestId) =>
        _scopes.Push(new[] { new KeyValuePair<string, object>("RequestId", requestId) });

    /// <inheritdoc/>
    public void Dispose()
    {
        // The entries are the test's to read after the deployment has stopped.
    }

    // The values of every scope open around the entry being written, the innermost
    // last, so an inner scope's value stands where two name the same one.
    private Dictionary<string, string> Open()
    {
        var open = new Dictionary<string, string>(StringComparer.Ordinal);

        _scopes.ForEachScope(
            (scope, values) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> carried)
                {
                    foreach (KeyValuePair<string, object> value in carried)
                    {
                        values[value.Key] = value.Value?.ToString() ?? string.Empty;
                    }
                }
            },
            open);

        return open;
    }

    /// <summary>
    /// One entry as it was written.
    /// </summary>
    /// <param name="Category">The category of the logger that wrote it.</param>
    /// <param name="Level">The level it was written at.</param>
    /// <param name="Line">Its message, followed by every value its state carries.</param>
    /// <param name="Scope">The values of the scopes open around it, by name.</param>
    internal sealed record Entry(
        string Category,
        LogLevel Level,
        string Line,
        IReadOnlyDictionary<string, string> Scope);

    private sealed class Writer(string category, EntriesInMemory entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            entries._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries._written.Enqueue(new Entry(
                category,
                logLevel,
                string.Join(
                    ' ',
                    [
                        formatter(state, exception),
                        .. state is IEnumerable<KeyValuePair<string, object?>> values
                            ? values.Select(value => value.Key + "=" + value.Value)
                            : [],
                    ]),
                entries.Open()));
    }
}
