using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The statements every connection sent while one operation ran, the library's own
/// included, read from the traces the database driver writes.
/// </summary>
/// <remarks>
/// Implements CONV-TEST-002 for AUTHZ-GATE-005 AC1 and AUTHZ-CONCEAL-002 AC2. The library's context takes no
/// interceptor from a host, so its statements are counted where the driver traces
/// them, and only those traced under the operation this instance started, since other
/// test classes run beside it.
/// </remarks>
internal sealed class TracedStatements : IDisposable
{
    private const string Driver = "Npgsql";

    private readonly ActivityListener _listener;

    private readonly Activity _operation;

    private readonly ConcurrentQueue<string> _texts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TracedStatements"/> class and
    /// starts the operation whose statements are counted.
    /// </summary>
    public TracedStatements()
    {
        _operation = new Activity("operation");
        _ = _operation.SetIdFormat(ActivityIdFormat.W3C).Start();
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is Driver,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Stopped,
        };

        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// The statements sent under the operation so far.
    /// </summary>
    public int Statements => _texts.Count;

    /// <summary>
    /// The text of each statement sent under the operation so far, in the order each
    /// ended.
    /// </summary>
    public IReadOnlyList<string> Texts => [.. _texts];

    /// <inheritdoc/>
    public void Dispose()
    {
        _operation.Dispose();
        _listener.Dispose();
    }

    private void Stopped(Activity activity)
    {
        if (activity.Source.Name is Driver
            && activity.TraceId == _operation.TraceId
            && activity.Kind is ActivityKind.Client
            && activity.GetTagItem("db.query.text") is string text)
        {
            _texts.Enqueue(text);
        }
    }
}
