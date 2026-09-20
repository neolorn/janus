using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// Where the conditions already raised are remembered, so a test can prove one
/// sustained attack produces one alert.
/// </summary>
internal sealed class AlertLedgerInMemory : IAlertLedger
{
    private readonly Dictionary<string, DateTimeOffset> _raised = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<bool> FirstAsync(
        string key,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (_raised.TryGetValue(key, out DateTimeOffset standing) && standing > at - window)
        {
            return ValueTask.FromResult(false);
        }

        _raised[key] = at;

        return ValueTask.FromResult(true);
    }
}
