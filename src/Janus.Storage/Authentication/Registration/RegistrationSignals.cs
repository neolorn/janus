using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Npgsql;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// What a waiting stream waits on, listening to the database channel for every stream
/// the instance holds open.
/// </summary>
/// <param name="connectionString">The credential the listening connection opens under.</param>
/// <param name="time">The clock the interval is waited on.</param>
/// <remarks>
/// Implements REG-SESS-003 and FE-VER-001. One connection listens for all of them,
/// because a connection for each would be a waiting screen costing a connection. A wait
/// ends on the channel or on the interval, whichever comes first, so an instance whose
/// listening connection has dropped reads the state back on the interval and loses
/// nothing but the promptness of a press.
/// </remarks>
internal sealed class RegistrationSignals(string connectionString, TimeProvider time)
    : IRegistrationSignals, IAsyncDisposable
{
    private readonly ConcurrentDictionary<RegistrationSessionId, TaskCompletionSource> _waiting =
        new();

    private readonly CancellationTokenSource _stopping = new();

    private readonly SemaphoreSlim _starting = new(1, 1);

    private Task _listening = Task.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask WaitAsync(
        RegistrationSessionId session,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        await ListeningAsync(cancellationToken).ConfigureAwait(false);

        TaskCompletionSource waiter = _waiting.GetOrAdd(
            session,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        _ = await Task
            .WhenAny(waiter.Task, Task.Delay(interval, time, cancellationToken))
            .ConfigureAwait(false);

        if (!waiter.Task.IsCompleted)
        {
            // The interval came first. What this wait took is dropped only where it is
            // still the one standing, so a signal that arrived in between is not lost.
            _ = _waiting.TryRemove(
                new KeyValuePair<RegistrationSessionId, TaskCompletionSource>(session, waiter));
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        _stopping.Dispose();
        _starting.Dispose();
    }

    private async ValueTask ListeningAsync(CancellationToken cancellationToken)
    {
        if (!_listening.IsCompleted)
        {
            return;
        }

        await _starting.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_listening.IsCompleted)
            {
                // The connection the last one held has ended, dropped by the database or
                // closed under it. Its failure is taken here and nowhere else, because a
                // wait ends on the interval as well: the stream reads the state back and
                // the next wait opens the connection again.
                _ = _listening.Exception;

                _listening = Task.Run(
                    () => ListenAsync(_stopping.Token),
                    CancellationToken.None);
            }
        }
        finally
        {
            _ = _starting.Release();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = new(connectionString);

        await using (connection.ConfigureAwait(false))
        {
            connection.Notification += (_, heard) => Signalled(heard.Payload);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            NpgsqlCommand listen = new("LISTEN " + RegistrationChannel.Name, connection);

            await using (listen.ConfigureAwait(false))
            {
                _ = await listen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Signalled(string payload)
    {
        if (!Guid.TryParse(payload, out Guid session))
        {
            return;
        }

        if (_waiting.TryRemove(new RegistrationSessionId(session), out TaskCompletionSource? waiter))
        {
            _ = waiter.TrySetResult();
        }
    }
}
