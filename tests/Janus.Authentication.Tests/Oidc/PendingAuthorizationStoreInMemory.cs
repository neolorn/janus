using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The authorizations browsers have waiting, held in memory and bound as the table
/// binds them: to a browser's pre-authentication session, then to a session record.
/// </summary>
internal sealed class PendingAuthorizationStoreInMemory : IPendingAuthorizationStore
{
    private readonly Dictionary<string, PendingAuthorization> _browsers = new(StringComparer.Ordinal);
    private readonly Dictionary<SessionId, PendingAuthorization> _records = [];

    /// <summary>
    /// How many the store holds, wherever each is bound.
    /// </summary>
    public int Count => _browsers.Count + _records.Count;

    /// <inheritdoc/>
    public ValueTask KeepAsync(PendingAuthorization pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        _browsers[Key(pending.PreAuthentication)] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> CarryAsync(
        byte[] preAuthentication,
        SessionId record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_browsers.Remove(Key(preAuthentication), out PendingAuthorization? pending))
        {
            return ValueTask.FromResult(false);
        }

        _records[record] = pending;

        return ValueTask.FromResult(!pending.HasExpired(now));
    }

    /// <inheritdoc/>
    public ValueTask<bool> WaitsAsync(SessionId record, DateTimeOffset now, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_records.TryGetValue(record, out PendingAuthorization? pending) && !pending.HasExpired(now));

    /// <inheritdoc/>
    public ValueTask<string?> TakeAsync(SessionId record, DateTimeOffset now, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _records.Remove(record, out PendingAuthorization? pending) && !pending.HasExpired(now)
                ? pending.Parameters
                : null);

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> browsers = [.. _browsers.Where(held => held.Value.HasExpired(now)).Select(held => held.Key)];
        List<SessionId> records = [.. _records.Where(held => held.Value.HasExpired(now)).Select(held => held.Key)];

        browsers.ForEach(key => _browsers.Remove(key));
        records.ForEach(key => _records.Remove(key));

        return ValueTask.FromResult(browsers.Count + records.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
