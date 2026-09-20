using System;

namespace Janus.Authentication.Oidc;

/// <summary>
/// One key pair the deployment signs tokens with: the private material as the store
/// holds it, the public material as the key set publishes it, and when it stops
/// signing and stops being published.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The private material never leaves the
/// store unwrapped, and a key is retired only after every token it could have signed
/// has expired.
/// </remarks>
internal sealed class SigningKey
{
    private SigningKey(
        string keyId,
        string algorithm,
        byte[] publicKey,
        DateTimeOffset createdAt,
        DateTimeOffset? supersededAt,
        DateTimeOffset? retiresAt)
    {
        KeyId = keyId;
        Algorithm = algorithm;
        PublicKey = publicKey;
        CreatedAt = createdAt;
        SupersededAt = supersededAt;
        RetiresAt = retiresAt;
    }

    /// <summary>What a token's header names.</summary>
    public string KeyId { get; }

    /// <summary>What it signs with.</summary>
    public string Algorithm { get; }

    /// <summary>The public key in subject public key information format.</summary>
    public byte[] PublicKey { get; }

    /// <summary>When it began signing.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When a newer key took over the signing, and nothing while it signs.</summary>
    public DateTimeOffset? SupersededAt { get; private set; }

    /// <summary>
    /// When it leaves the published set, which is the overlap after it stopped
    /// signing, and nothing while it signs.
    /// </summary>
    public DateTimeOffset? RetiresAt { get; private set; }

    /// <summary>Whether it is the key signing now.</summary>
    public bool IsSigning => SupersededAt is null;

    /// <summary>
    /// Creates one.
    /// </summary>
    /// <param name="keyId">What a token's header will name.</param>
    /// <param name="algorithm">What it signs with.</param>
    /// <param name="publicKey">The public key in subject public key information format.</param>
    /// <param name="createdAt">When it begins signing.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The public key is absent.</exception>
    public static SigningKey Create(
        string keyId,
        string algorithm,
        byte[] publicKey,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new SigningKey(keyId, algorithm, publicKey, createdAt, supersededAt: null, retiresAt: null);
    }

    /// <summary>
    /// The key as the store holds it.
    /// </summary>
    /// <param name="keyId">What a token's header names.</param>
    /// <param name="algorithm">What it signs with.</param>
    /// <param name="publicKey">The public key in subject public key information format.</param>
    /// <param name="createdAt">When it began signing.</param>
    /// <param name="supersededAt">When it stopped signing, or nothing.</param>
    /// <param name="retiresAt">When it leaves the published set, or nothing.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The public key is absent.</exception>
    public static SigningKey Existing(
        string keyId,
        string algorithm,
        byte[] publicKey,
        DateTimeOffset createdAt,
        DateTimeOffset? supersededAt,
        DateTimeOffset? retiresAt)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new SigningKey(keyId, algorithm, publicKey, createdAt, supersededAt, retiresAt);
    }

    /// <summary>
    /// A newer key took over the signing, so this one stays published for the overlap
    /// and then leaves the set (AUTH-KEY-001).
    /// </summary>
    /// <param name="at">When the newer key began signing.</param>
    /// <param name="overlap">How long tokens this key signed may still be presented.</param>
    public void Supersede(DateTimeOffset at, TimeSpan overlap)
    {
        SupersededAt = at;
        RetiresAt = at + overlap;
    }

    /// <summary>
    /// Whether it has left the published set.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has retired.</returns>
    public bool HasRetired(DateTimeOffset now) => RetiresAt is DateTimeOffset retires && now >= retires;

    /// <summary>
    /// Whether it is older than the cadence, which is what makes a rotation due.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cadence">How often a key is replaced.</param>
    /// <returns>Whether a new key should take over.</returns>
    public bool IsDue(DateTimeOffset now, TimeSpan cadence) => now >= CreatedAt + cadence;
}
