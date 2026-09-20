using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// What browsers carry before they hold a session, held in memory and keyed as the
/// table is: by what the cookie's token fingerprints to.
/// </summary>
internal sealed class PreAuthenticationStoreInMemory : IPreAuthenticationStore
{
    private readonly Dictionary<string, PreAuthentication> _contacts = new(StringComparer.Ordinal);

    /// <summary>
    /// Every first contact the store holds.
    /// </summary>
    public IReadOnlyCollection<PreAuthentication> All => _contacts.Values;

    /// <inheritdoc/>
    public ValueTask<PreAuthentication?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_contacts.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask AddAsync(PreAuthentication preAuthentication, CancellationToken cancellationToken)
    {
        _contacts[Key(preAuthentication.Fingerprint)] = preAuthentication;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(PreAuthentication preAuthentication, CancellationToken cancellationToken)
    {
        _contacts[Key(preAuthentication.Fingerprint)] = preAuthentication;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        _ = _contacts.Remove(Key(fingerprint));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> gone = [.. _contacts
            .Where(contact => contact.Value.HasExpired(now))
            .Select(contact => contact.Key)];

        foreach (string key in gone)
        {
            _ = _contacts.Remove(key);
        }

        return ValueTask.FromResult(gone.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
