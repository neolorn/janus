using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests;

/// <summary>
/// Every line a deployment logs, whoever logs it and at whatever level, with each value
/// the line carries, so a test can say what never reaches a log (CONV-LOG-003).
/// </summary>
internal sealed class LogsInMemory : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>
    /// Every line and scope written, in order, each followed by the values it carries.
    /// </summary>
    public IReadOnlyCollection<string> Lines => _lines;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Writer(categoryName, _lines);

    /// <inheritdoc/>
    public void Dispose()
    {
        // The lines are the test's to read after the deployment has stopped.
    }

    // A line as written: the category, the message, every value the state carries and
    // the exception, if one was handed over.
    private static string Written(string category, string message, object? state, Exception? exception) =>
        string.Join(
            ' ',
            [
                category,
                message,
                .. state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.Select(value => value.Key + "=" + value.Value)
                    : [],
                exception?.ToString() ?? string.Empty,
            ]);

    private sealed class Writer(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            lines.Enqueue(Written(category, state.ToString() ?? string.Empty, state, exception: null));

            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(Written(category, formatter(state, exception), state, exception));
    }
}
