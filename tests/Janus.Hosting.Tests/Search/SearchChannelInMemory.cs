using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;

namespace Janus.Hosting.Tests.Search;

/// <summary>
/// The staff search's channel as a test drives it: a change is announced to the one
/// listening, and the listening connection is lost when the test says so.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class SearchChannelInMemory : ISearchChannel
{
    private readonly Lock _gate = new();
    private Action<SubjectId>? _changed;
    private TaskCompletionSource? _listening;

    /// <summary>
    /// How many times the channel has been listened on.
    /// </summary>
    public int Listens { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Task> ListenAsync(Action<SubjectId> changed, CancellationToken cancellationToken)
    {
        var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            _changed = changed;
            _listening = listening;
            Listens++;
        }

        _ = cancellationToken.Register(() => listening.TrySetCanceled(cancellationToken));

        return ValueTask.FromResult(listening.Task);
    }

    /// <summary>
    /// Announces a change of a subject, as a write's notification does.
    /// </summary>
    /// <param name="subject">The subject.</param>
    public void Announce(SubjectId subject)
    {
        Action<SubjectId>? changed;

        lock (_gate)
        {
            changed = _changed;
        }

        changed?.Invoke(subject);
    }

    /// <summary>
    /// Loses the listening connection, so no change reaches the listener until it listens
    /// again.
    /// </summary>
    public void Lose()
    {
        TaskCompletionSource? listening;

        lock (_gate)
        {
            listening = _listening;
            _changed = null;
        }

        _ = listening?.TrySetException(new InvalidOperationException("The listening connection was lost."));
    }
}
