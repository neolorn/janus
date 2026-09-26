using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Where failed attempts are counted, holding each scope's counter so a test can
/// read what one attempt raised and what a success forgot.
/// </summary>
internal sealed class ThrottleLedgerInMemory : IThrottleLedger
{
    private readonly Dictionary<(ThrottleScope Scope, string Key), ThrottleCounter> _counters = [];

    /// <summary>
    /// The scopes a counter stands for.
    /// </summary>
    public IReadOnlyCollection<(ThrottleScope Scope, string Key)> Counted => _counters.Keys;

    /// <inheritdoc/>
    public ValueTask<ThrottleCounter?> FindAsync(
        ThrottleScope scope,
        string key,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _counters.TryGetValue((scope, key), out ThrottleCounter counter)
                ? counter
                : (ThrottleCounter?)null);

    /// <inheritdoc/>
    public ValueTask FailedAsync(
        ThrottleScope scope,
        string key,
        int standing,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _counters[(scope, key)] = new ThrottleCounter(standing + 1, at);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(ThrottleScope scope, string key, CancellationToken cancellationToken)
    {
        _counters.Remove((scope, key));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public byte[] Identify(string identifier) => SHA256.HashData(Encoding.UTF8.GetBytes(identifier));
}
