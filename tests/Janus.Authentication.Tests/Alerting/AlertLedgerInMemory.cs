using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// Where the conditions already raised are remembered, so a test can prove one
/// sustained attack produces one alert, and a raise whose hand-over never answered is
/// handed over again.
/// </summary>
internal sealed class AlertLedgerInMemory : IAlertLedger
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, Claim> _raised = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<bool> FirstAsync(
        string key,
        RaisedAlertId raise,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_raised.TryGetValue(key, out Claim? standing)
                && standing.At > at - window
                && !(standing.Raise == raise && standing.Unanswered))
            {
                return ValueTask.FromResult(false);
            }

            _raised[key] = new Claim(at, raise, Unanswered: true);

            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> TakenAsync(string key, RaisedAlertId raise, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_raised.TryGetValue(key, out Claim? standing) && standing.Raise == raise)
            {
                _raised[key] = standing with { Unanswered = false };

                return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> ForgetAsync(string key, RaisedAlertId raise, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_raised.TryGetValue(key, out Claim? standing) && standing.Raise == raise && standing.Unanswered)
            {
                return ValueTask.FromResult(_raised.Remove(key));
            }

            return ValueTask.FromResult(false);
        }
    }

    // A key's claim: when the raise it was made for fired, which raise, and whether the
    // handler has yet to take it.
    private sealed record Claim(DateTimeOffset At, RaisedAlertId Raise, bool Unanswered);
}
