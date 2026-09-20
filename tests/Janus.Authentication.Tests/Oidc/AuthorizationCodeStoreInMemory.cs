using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The codes waiting to be exchanged, held in memory.
/// </summary>
internal sealed class AuthorizationCodeStoreInMemory : IAuthorizationCodeStore
{
    private readonly Dictionary<string, AuthorizationCode> _codes = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<AuthorizationCode?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_codes.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask AddAsync(AuthorizationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        _codes[Key(code.Fingerprint)] = code;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(AuthorizationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        _codes[Key(code.Fingerprint)] = code;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> lapsed = [.. _codes
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)];

        foreach (string key in lapsed)
        {
            _ = _codes.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
