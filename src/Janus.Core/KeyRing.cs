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
/// or the command ends; it is never filled again. It is filled in steps (D-176): every
/// secret but the mail server's, after which those are lent; then, once the start has
/// chosen the mail server in use, the mail server's where the adapter is chosen, after
/// which the ring is complete and that one is lent too.
/// </remarks>
internal sealed class KeyRing : IKeyRing
{
    /// <summary>
    /// The name the mail server's key is refused under.
    /// </summary>
    internal const string MailServerSecret = "mailServerSecret";

    private const int Filling = 0;

    private const int Filled = 1;

    private const int Complete = 2;

    private const int Cleared = 3;

    private readonly Dictionary<string, HeldCredential> _credentials = new(StringComparer.Ordinal);

    private byte[]? _mailServerSecret;

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

            if (_mailServerSecret is byte[] secret)
            {
                yield return secret;
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

    /// <inheritdoc/>
    public Result<TValue> BorrowMailServerSecret<TValue>(Func<ReadOnlyMemory<byte>, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        switch (Volatile.Read(ref _state))
        {
            case Filling or Filled:
                throw new InvalidOperationException(
                    "The mail server's key is read before the start chose the mail server in use.");
            case Cleared:
                throw new InvalidOperationException("The key ring is read after it was cleared.");
        }

        return _mailServerSecret is byte[] secret
            ? Result.Success(use(secret))
            : Result.Failure<TValue>(Unavailable(MailServerSecret));
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
    /// Holds the mail server's management key, copied into an array of the ring's own.
    /// </summary>
    /// <param name="secret">What the source answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The ring is not at the step that reads the mail server's key.
    /// </exception>
    internal void HoldMailServerSecret(ReadOnlyMemory<byte> secret)
    {
        if (Volatile.Read(ref _state) is not Filled || _mailServerSecret is not null)
        {
            throw new InvalidOperationException("The mail server's key is held once, after the other secrets.");
        }

        byte[] material = GC.AllocateArray<byte>(secret.Length, pinned: true);

        secret.Span.CopyTo(material);

        _mailServerSecret = material;
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
    /// Marks the ring complete, once the start has chosen the mail server in use, after
    /// which it lends the mail server's key too.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is complete already.</exception>
    internal void Completed()
    {
        if (Interlocked.CompareExchange(ref _state, Complete, Filled) is not Filled)
        {
            throw new InvalidOperationException("The key ring is completed once, after it is filled.");
        }
    }

    /// <summary>
    /// Clears every array the ring holds, after which a read is a fault.
    /// </summary>
    internal void Clear()
    {
        Volatile.Write(ref _state, Cleared);

        foreach (byte[] held in Arrays)
        {
            CryptographicOperations.ZeroMemory(held);
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
