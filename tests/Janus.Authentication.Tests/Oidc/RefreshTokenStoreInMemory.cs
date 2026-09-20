using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The refresh tokens, held in memory by family.
/// </summary>
internal sealed class RefreshTokenStoreInMemory : IRefreshTokenStore
{
    private readonly Dictionary<string, RefreshToken> _tokens = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<RefreshToken?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tokens.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RefreshToken>> OfAsync(
        RefreshFamilyId family,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<RefreshToken>>(
            [.. _tokens.Values.Where(token => token.Family == family).OrderBy(token => token.IssuedAt)]);

    /// <inheritdoc/>
    public ValueTask AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        _tokens[Key(token.Fingerprint)] = token;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        _tokens[Key(token.Fingerprint)] = token;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveFamilyAsync(RefreshFamilyId family, CancellationToken cancellationToken)
    {
        foreach (string key in _tokens
            .Where(entry => entry.Value.Family == family)
            .Select(entry => entry.Key)
            .ToList())
        {
            _ = _tokens.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> lapsed = [.. _tokens
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)];

        foreach (string key in lapsed)
        {
            _ = _tokens.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
