using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// An alert handler that keeps every alert handed to it, readable by condition, and
/// fails a hand-over only where the test says so.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003 (D-225). An alert it fails is not kept, and the library hands
/// it over again at the next pass of <c>alert-dispatch</c>.
/// </remarks>
public sealed class InMemoryAlerts : IAlertHandler
{
    private readonly ConcurrentQueue<AlertRaised> _taken = new();

    private volatile bool _failing;

    /// <summary>
    /// Every alert taken, in the order it was taken.
    /// </summary>
    public IReadOnlyList<AlertRaised> Taken => [.. _taken];

    /// <summary>
    /// Every alert of one condition taken, in the order it was taken.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The alerts.</returns>
    public IReadOnlyList<AlertRaised> Of(AlertCondition condition) =>
        [.. _taken.Where(alert => alert.Condition == condition)];

    /// <summary>
    /// Fails every hand-over from now until <see cref="AcceptHandOvers"/>.
    /// </summary>
    public void FailHandOvers() => _failing = true;

    /// <summary>
    /// Takes hand-overs again.
    /// </summary>
    public void AcceptHandOvers() => _failing = false;

    /// <inheritdoc/>
    public ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (_failing)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        _taken.Enqueue(alert);

        return ValueTask.FromResult(Result.Success());
    }
}
