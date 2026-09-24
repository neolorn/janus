using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// The raised conditions waiting for the alert channels, held in the order raised.
/// </summary>
internal sealed class RaisedAlertsInMemory : IRaisedAlerts
{
    private readonly List<RaisedAlert> _waiting = [];

    /// <summary>
    /// What is waiting, oldest first.
    /// </summary>
    public IReadOnlyList<RaisedAlert> Waiting => [.. _waiting];

    /// <inheritdoc/>
    public ValueTask AddAsync(RaisedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        _waiting.Add(alert);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RaisedAlert>> OldestAsync(int count, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<RaisedAlert>>([.. _waiting.OrderBy(alert => alert.Id.Value).Take(count)]);

    /// <inheritdoc/>
    public ValueTask RemoveAsync(RaisedAlertId alert, CancellationToken cancellationToken)
    {
        _ = _waiting.RemoveAll(waiting => waiting.Id == alert);

        return ValueTask.CompletedTask;
    }
}
