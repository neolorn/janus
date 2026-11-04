using System;
using System.Threading;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting.Tests.Search;

/// <summary>
/// A host's lifetime as a test drives it: the host has started when the test says so.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class HostLifetimeInMemory : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _started = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationTokenSource _stopped = new();

    /// <inheritdoc/>
    public CancellationToken ApplicationStarted => _started.Token;

    /// <inheritdoc/>
    public CancellationToken ApplicationStopping => _stopping.Token;

    /// <inheritdoc/>
    public CancellationToken ApplicationStopped => _stopped.Token;

    /// <summary>
    /// Says the host has started.
    /// </summary>
    public void Start() => _started.Cancel();

    /// <inheritdoc/>
    public void StopApplication() => _stopping.Cancel();

    /// <inheritdoc/>
    public void Dispose()
    {
        _started.Dispose();
        _stopping.Dispose();
        _stopped.Dispose();
    }
}
