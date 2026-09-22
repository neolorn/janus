using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// The channel as a test raises it, with the same interval the real one falls back to.
/// </summary>
/// <param name="time">The clock the interval is waited on.</param>
internal sealed class RegistrationSignalsInMemory(TimeProvider time) : IRegistrationSignals
{
    private readonly ConcurrentDictionary<RegistrationSessionId, TaskCompletionSource> _waiting =
        new();

    /// <summary>
    /// Signals a session, as a transaction that changed it would.
    /// </summary>
    /// <param name="session">The session that changed.</param>
    public void Raise(RegistrationSessionId session)
    {
        if (_waiting.TryRemove(session, out TaskCompletionSource? waiter))
        {
            _ = waiter.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public async ValueTask WaitAsync(
        RegistrationSessionId session,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource waiter = _waiting.GetOrAdd(
            session,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        _ = await Task
            .WhenAny(waiter.Task, Task.Delay(interval, time, cancellationToken))
            .ConfigureAwait(false);
    }
}
