using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;

namespace Janus.Authentication.Tests.Callbacks;

/// <summary>
/// The claimed provider events, holding each claim so a test can read what is held.
/// </summary>
internal sealed class CallbackEventsInMemory : ICallbackEvents
{
    private readonly List<(string Callback, byte[] Identifier)> _claimed = [];

    /// <summary>
    /// How many claims are held.
    /// </summary>
    public int Held => _claimed.Count;

    /// <inheritdoc/>
    public ValueTask<bool> ClaimAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (_claimed.Any(claimed => Same(claimed, callback, identifier)))
        {
            return ValueTask.FromResult(false);
        }

        _claimed.Add((callback, identifier));

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask ReleaseAsync(string callback, byte[] identifier, CancellationToken cancellationToken)
    {
        _ = _claimed.RemoveAll(claimed => Same(claimed, callback, identifier));

        return ValueTask.CompletedTask;
    }

    private static bool Same((string Callback, byte[] Identifier) claimed, string callback, byte[] identifier) =>
        string.Equals(claimed.Callback, callback, StringComparison.Ordinal)
        && claimed.Identifier.AsSpan().SequenceEqual(identifier);
}
