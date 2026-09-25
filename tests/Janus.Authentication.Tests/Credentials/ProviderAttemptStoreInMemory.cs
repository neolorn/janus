using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;

namespace Janus.Authentication.Tests.Credentials;

/// <summary>
/// The round trips to social providers browsers have in flight, held in memory. One
/// stands per browser, as the unique indexes hold it.
/// </summary>
internal sealed class ProviderAttemptStoreInMemory : IProviderAttemptStore
{
    private readonly Dictionary<string, ProviderAttempt> _bound = new(StringComparer.Ordinal);

    /// <summary>
    /// How many round trips are in flight.
    /// </summary>
    public int Count => _bound.Count;

    /// <inheritdoc/>
    public ValueTask BindAsync(
        ProviderBinding binding,
        ProviderAttempt attempt,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        _bound[Key(binding)] = attempt;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ProviderAttempt?> TakeAsync(ProviderBinding binding, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_bound.Remove(Key(binding), out ProviderAttempt? attempt) ? attempt : null);

    // The fingerprint is compared by what it holds, as the column is.
    private static string Key(ProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return binding.Session is SessionId session
            ? "session:" + session.Value
            : "preauthentication:" + Convert.ToHexString(binding.PreAuthentication!);
    }
}
