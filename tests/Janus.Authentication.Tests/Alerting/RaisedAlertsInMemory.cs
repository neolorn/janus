using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// The raised conditions as a test reads them, with the claim each pass takes.
/// </summary>
internal sealed class RaisedAlertsInMemory : IRaisedAlerts
{
    private readonly List<RaisedAlert> _waiting = [];
    private readonly Dictionary<RaisedAlertId, DateTimeOffset> _claims = [];

    /// <summary>
    /// What is waiting to be carried.
    /// </summary>
    public IReadOnlyList<RaisedAlert> Waiting => [.. _waiting];

    /// <summary>
    /// Stands in for another pass that takes the row over, as one would once the claim
    /// on it had timed out.
    /// </summary>
    /// <param name="alert">The row.</param>
    /// <param name="until">When the other pass's claim times out.</param>
    public void TakeOver(RaisedAlertId alert, DateTimeOffset until) => _claims[alert] = until;

    /// <inheritdoc/>
    public ValueTask AddAsync(RaisedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        _waiting.Add(alert);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RaisedAlert>> OldestAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<RaisedAlert>>(
        [
            .. _waiting
                .Where(alert => !_claims.TryGetValue(alert.Id, out DateTimeOffset until) || until <= now)
                .OrderBy(alert => alert.Id.Value)
                .Take(count),
        ]);

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> ClaimAsync(
        RaisedAlertId alert,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!_waiting.Exists(waiting => waiting.Id == alert)
            || (_claims.TryGetValue(alert, out DateTimeOffset until) && until > now))
        {
            return ValueTask.FromResult<DateTimeOffset?>(null);
        }

        _claims[alert] = now + timeout;

        return ValueTask.FromResult<DateTimeOffset?>(now + timeout);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RemoveAsync(RaisedAlertId alert, DateTimeOffset claim, CancellationToken cancellationToken)
    {
        if (!Holds(alert, claim))
        {
            return ValueTask.FromResult(false);
        }

        _ = _claims.Remove(alert);

        return ValueTask.FromResult(_waiting.RemoveAll(waiting => waiting.Id == alert) > 0);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(RaisedAlertId alert, DateTimeOffset claim, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Holds(alert, claim) && _claims.Remove(alert));

    private bool Holds(RaisedAlertId alert, DateTimeOffset claim) =>
        _claims.TryGetValue(alert, out DateTimeOffset until) && until == claim;
}
