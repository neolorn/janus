using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;
using Npgsql;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// What the staff search's index listens to: the database channel every write that
/// changes what it holds is announced on.
/// </summary>
/// <param name="connectionString">The credential the listening connection opens under.</param>
/// <remarks>
/// Implements IDN-SRCH-003 and OPS-DATA-003 (D-256). Listen and notify have no
/// abstraction above ADO.NET, and a listening connection outlives every operation, so it
/// is opened here rather than taken from the accessor, and it runs nothing but the
/// LISTEN. What it hears is a subject's identifier; anything else is not a subject and is
/// passed over.
/// </remarks>
internal sealed class SearchSignals(string connectionString) : ISearchChannel
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The callback is absent.</exception>
    public async ValueTask<Task> ListenAsync(Action<SubjectId> changed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changed);

        var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task listened = ListenAsync(changed, listening, cancellationToken);

        // A connection that never listened is answered as one already lost, its fault on
        // the work returned, for the caller to watch as it watches a loss.
        _ = await Task.WhenAny(listening.Task, listened).ConfigureAwait(false);

        return listened;
    }

    private async Task ListenAsync(
        Action<SubjectId> changed,
        TaskCompletionSource listening,
        CancellationToken cancellationToken)
    {
        // OPS-DATA-003: listen/notify has no abstraction above ADO.NET, and a listening
        // connection outlives every operation, so it is opened here rather than taken
        // from the accessor, and it runs nothing but the LISTEN.
        var connection = new NpgsqlConnection(connectionString);

        await using (connection.ConfigureAwait(false))
        {
            connection.Notification += (_, heard) =>
            {
                if (SubjectId.TryParse(heard.Payload, provider: null, out SubjectId subject))
                {
                    changed(subject);
                }
            };

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            NpgsqlCommand listen = new("LISTEN " + SearchChannel.Name, connection);

            await using (listen.ConfigureAwait(false))
            {
                _ = await listen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            listening.SetResult();

            while (!cancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
