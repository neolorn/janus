using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace Janus.Core;

/// <summary>
/// The key ring, holding each secret in a pinned array allocated once and lending it
/// per use.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007 and D-171. Pinning keeps the runtime from moving an array,
/// so clearing it removes the only copy the library holds. The ring is filled once, at
/// startup or at the start of a command, and cleared once, when the application stops
/// or the command ends; it is never filled again.
/// </remarks>
internal sealed class KeyRing : IKeyRing
{
    private const int Filling = 0;

    private const int Filled = 1;

    private const int Cleared = 2;

    private readonly Dictionary<string, HeldCredential> _credentials = new(StringComparer.Ordinal);

    private int _state;

    /// <summary>
    /// Every array the ring holds, so a test can see its clearing leave them zero.
    /// </summary>
    internal IEnumerable<byte[]> Arrays
    {
        get
        {
            foreach (HeldCredential held in _credentials.Values)
            {
                yield return held.Material;
            }
        }
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowProviderCredential<TValue>(string provider, Func<ProviderCredential, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(use);

        Readable();

        if (!_credentials.TryGetValue(provider, out HeldCredential? held))
        {
            return Result.Failure<TValue>(Unavailable(Named(provider)));
        }

        ProviderCredential lent = held.Issuer is string issuer && held.KeyId is string keyId
            ? ProviderCredential.Signed(issuer, keyId, held.Material)
            : ProviderCredential.Secret(held.Material);

        return Result.Success(use(lent));
    }

    /// <summary>
    /// The name a social provider's credential is refused under.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <returns>The name, <c>socialProvider.&lt;provider&gt;</c>.</returns>
    internal static string Named(string provider) => "socialProvider." + provider;

    /// <summary>
    /// The refusal of a secret the ring cannot answer.
    /// </summary>
    /// <param name="key">The secret, as <c>details.key</c> names it.</param>
    /// <returns>The failure.</returns>
    internal static Error Unavailable(string key) =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(key));

    /// <summary>
    /// Holds a social provider's credential, copied into an array of the ring's own.
    /// </summary>
    /// <param name="provider">The provider's name, as the secret source was asked for it.</param>
    /// <param name="credential">What the source answered.</param>
    /// <exception cref="InvalidOperationException">The ring is no longer being filled.</exception>
    internal void Hold(string provider, ProviderCredential credential)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(credential);

        if (Volatile.Read(ref _state) is not Filling)
        {
            throw new InvalidOperationException("The key ring is filled once.");
        }

        byte[] material = GC.AllocateArray<byte>(credential.Material.Length, pinned: true);

        credential.Material.Span.CopyTo(material);

        _credentials[provider] = new HeldCredential(credential.Issuer, credential.KeyId, material);
    }

    /// <summary>
    /// Marks the ring filled, after which it lends what it holds.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ring is no longer being filled.</exception>
    internal void Fill()
    {
        if (Interlocked.CompareExchange(ref _state, Filled, Filling) is not Filling)
        {
            throw new InvalidOperationException("The key ring is filled once.");
        }
    }

    /// <summary>
    /// Clears every array the ring holds, after which a read is a fault.
    /// </summary>
    internal void Clear()
    {
        Volatile.Write(ref _state, Cleared);

        foreach (HeldCredential held in _credentials.Values)
        {
            CryptographicOperations.ZeroMemory(held.Material);
        }
    }

    private void Readable()
    {
        switch (Volatile.Read(ref _state))
        {
            case Filling:
                throw new InvalidOperationException("The key ring is read before it is filled.");
            case Cleared:
                throw new InvalidOperationException("The key ring is read after it was cleared.");
        }
    }

    private sealed record HeldCredential(string? Issuer, string? KeyId, byte[] Material);
}
