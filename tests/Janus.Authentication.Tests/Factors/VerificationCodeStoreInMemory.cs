using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The codes outstanding, held in memory as the table holds them: one row per holder,
/// which is what a code is issued against.
/// </summary>
internal sealed class VerificationCodeStoreInMemory : IVerificationCodeStore
{
    private readonly Dictionary<string, VerificationCode> _codes = new(StringComparer.Ordinal);

    /// <summary>
    /// Every code the store holds.
    /// </summary>
    public IReadOnlyCollection<VerificationCode> All => _codes.Values;

    /// <inheritdoc/>
    public ValueTask<VerificationCode?> FindAsync(
        byte[] holder,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_codes.GetValueOrDefault(Key(holder)));

    /// <inheritdoc/>
    public ValueTask AddAsync(VerificationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        _codes[Key(code.Holder)] = code;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(VerificationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!_codes.ContainsKey(Key(code.Holder)))
        {
            throw new InvalidOperationException("The code has no row to carry the change.");
        }

        _codes[Key(code.Holder)] = code;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(byte[] holder, CancellationToken cancellationToken)
    {
        _ = _codes.Remove(Key(holder));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        string[] gone = [.. _codes
            .Where(held => held.Value.ExpiresAt <= now)
            .Select(held => held.Key)];

        foreach (string key in gone)
        {
            _ = _codes.Remove(key);
        }

        return ValueTask.FromResult(gone.Length);
    }

    private static string Key(byte[] holder)
    {
        ArgumentNullException.ThrowIfNull(holder);

        return Convert.ToHexString(holder);
    }
}
