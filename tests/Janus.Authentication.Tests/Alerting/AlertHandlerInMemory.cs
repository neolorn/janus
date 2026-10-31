using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// The host's alert handler, keeping every alert it takes.
/// </summary>
internal sealed class AlertHandlerInMemory : IAlertHandler
{
    private readonly List<AlertRaised> _taken = [];

    /// <summary>
    /// Every alert taken, in the order it was taken.
    /// </summary>
    public IReadOnlyList<AlertRaised> Taken => [.. _taken];

    /// <inheritdoc/>
    public ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        _taken.Add(alert);

        return ValueTask.FromResult(Result.Success());
    }
}
