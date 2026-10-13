using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Registration;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// What a waiting stream waits on, listening to the database channel for every stream
/// the instance holds open.
/// </summary>
/// <param name="connectionString">The credential the listening connection opens under.</param>
/// <param name="scopes">Where the scope a lost channel is raised in comes from.</param>
/// <param name="time">The clock the interval is waited on.</param>
/// <remarks>
/// Implements REG-SESS-003, FE-VER-001 and OPS-OBS-002. One connection listens for all
/// of them, because a connection for each would be a waiting screen costing a
/// connection. A wait ends on the channel or on the interval, whichever comes first, so
/// an instance whose listening connection has dropped reads the state back on the
/// interval and loses nothing but the promptness of a press. A wait that begins while
/// the channel it opened is no longer listened on raises the loss itself, in a scope and
/// unit of work of its own, and opens the channel again.
/// </remarks>
internal sealed class RegistrationSignals(
    string connectionString,
    IServiceScopeFactory scopes,
    TimeProvider time)
    : IRegistrationSignals, IAsyncDisposable
{
    private const string Component = "registration-channel";

    private readonly ConcurrentDictionary<RegistrationSessionId, TaskCompletionSource> _waiting =
        new();

    private readonly CancellationTokenSource _stopping = new();

    private readonly SemaphoreSlim _starting = new(1, 1);

    // Nothing until the first wait opens the channel: a channel never opened is not lost.
    private Task? _listening;

    /// <inheritdoc/>
    public async ValueTask WaitAsync(
        RegistrationSessionId session,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (!await ListeningAsync(cancellationToken).ConfigureAwait(false))
        {
            await LostAsync(cancellationToken).ConfigureAwait(false);
        }

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

    // Whether the channel was listened on when the wait began. Where it was not, it is
    // opened, so the next wait finds it listening.
    private async ValueTask<bool> ListeningAsync(CancellationToken cancellationToken)
    {
        if (_listening is { IsCompleted: false })
        {
            return true;
        }

        await _starting.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_listening is { IsCompleted: false })
            {
                return true;
            }

            // The connection the last one held has ended, dropped by the database or
            // closed under it. Its failure is taken here and nowhere else, because a
            // wait ends on the interval as well: the stream reads the state back and
            // this wait opens the connection again.
            bool lost = _listening is not null;

            _ = _listening?.Exception;

            _listening = Task.Run(
                () => ListenAsync(_stopping.Token),
                CancellationToken.None);

            return !lost;
        }
        finally
        {
            _ = _starting.Release();
        }
    }

    // OPS-OBS-002: the loss is raised in a scope and unit of work of its own, which the
    // alert channels begin and commit, and the window of OPS-ALERT-002 folds one raised
    // by every wait of every stream into one alert.
    private async ValueTask LostAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result raised = await scope.ServiceProvider
                .GetRequiredService<IAlertChannels>()
                .RaiseAsync(
                    Alerts.Of(
                        AlertCondition.Degradation,
                        named: null,
                        time.GetUtcNow(),
                        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["component"] = JsonSerializer.SerializeToElement(Component),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            raised.Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        // OPS-DATA-003: listen/notify has no abstraction above ADO.NET, and a listening
        // connection outlives every operation, so it is opened here rather than taken
        // from the accessor, and it runs nothing but the LISTEN.
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
