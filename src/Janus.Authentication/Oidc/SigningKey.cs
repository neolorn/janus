using System;

namespace Janus.Authentication.Oidc;

/// <summary>
/// One key pair the deployment signs tokens with, as the database holds it: the public
/// key, whether the private key is still held, and the moments that decide what the key
/// does now.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. A key is made next, published and signing
/// nothing; becomes current, the one key that signs; is replaced, and stays published
/// through its overlap; is retired at the end of its overlap, its private key gone and
/// its public key kept, unpublished, for the provider's own tokens; and is removed once
/// the longest a session can last has passed since it was replaced. The first key of an
/// empty database is current at once, since no relying party can have cached a set.
/// </remarks>
internal sealed class SigningKey
{
    /// <summary>
    /// How long a next key is published before it signs (AUTH-KEY-001): the mail server
    /// refuses a key its cached set lacks while that set is under 300 seconds old.
    /// </summary>
    public static readonly TimeSpan Lead = TimeSpan.FromMinutes(5);

    /// <summary>
    /// What the overlap adds to the longest access-token lifetime a key signed under,
    /// for clock skew and a relying party's cached copy of the key set (AUTH-KEY-001).
    /// </summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    private SigningKey(
        string keyId,
        string algorithm,
        byte[] publicKey,
        DateTimeOffset publishedAt,
        DateTimeOffset? signingFrom,
        TimeSpan longestLifetime,
        DateTimeOffset? replacedAt,
        DateTimeOffset? overlapEndsAt,
        DateTimeOffset? keptUntil,
        bool holdsPrivateKey)
    {
        KeyId = keyId;
        Algorithm = algorithm;
        PublicKey = publicKey;
        PublishedAt = publishedAt;
        SigningFrom = signingFrom;
        LongestLifetime = longestLifetime;
        ReplacedAt = replacedAt;
        OverlapEndsAt = overlapEndsAt;
        KeptUntil = keptUntil;
        HoldsPrivateKey = holdsPrivateKey;
    }

    /// <summary>What a token's header names.</summary>
    public string KeyId { get; }

    /// <summary>What it signs with.</summary>
    public string Algorithm { get; }

    /// <summary>The public key in subject public key information format.</summary>
    public byte[] PublicKey { get; }

    /// <summary>When it was made, and so first published.</summary>
    public DateTimeOffset PublishedAt { get; }

    /// <summary>When it began signing, and nothing while it is the next key.</summary>
    public DateTimeOffset? SigningFrom { get; }

    /// <summary>The longest access-token lifetime under which it has signed an access token.</summary>
    public TimeSpan LongestLifetime { get; }

    /// <summary>When a newer key took over the signing, and nothing before.</summary>
    public DateTimeOffset? ReplacedAt { get; }

    /// <summary>
    /// When its overlap ends, stored at its replacement: it leaves the key set then and
    /// is retired.
    /// </summary>
    public DateTimeOffset? OverlapEndsAt { get; }

    /// <summary>
    /// When its public key stops being kept, stored at its replacement: the longest a
    /// session can last after it.
    /// </summary>
    public DateTimeOffset? KeptUntil { get; }

    /// <summary>Whether the database still holds its private key.</summary>
    public bool HoldsPrivateKey { get; }

    /// <summary>Whether it is the next key, published and signing nothing.</summary>
    public bool IsNext => SigningFrom is null;

    /// <summary>Whether it is the key that signs.</summary>
    public bool IsCurrent => SigningFrom is not null && ReplacedAt is null;

    /// <summary>
    /// A key made to sign after the current one, published from the moment it is made.
    /// </summary>
    /// <param name="keyId">What a token's header will name.</param>
    /// <param name="algorithm">What it signs with.</param>
    /// <param name="publicKey">The public key in subject public key information format.</param>
    /// <param name="at">When it is made.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The public key is absent.</exception>
    public static SigningKey Next(string keyId, string algorithm, byte[] publicKey, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new SigningKey(keyId, algorithm, publicKey, at, null, TimeSpan.Zero, null, null, null, holdsPrivateKey: true);
    }

    /// <summary>
    /// The first key of a database that holds none, which signs from the moment it is
    /// made (AUTH-KEY-001 AC8).
    /// </summary>
    /// <param name="keyId">What a token's header will name.</param>
    /// <param name="algorithm">What it signs with.</param>
    /// <param name="publicKey">The public key in subject public key information format.</param>
    /// <param name="at">When it is made.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The public key is absent.</exception>
    public static SigningKey First(string keyId, string algorithm, byte[] publicKey, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new SigningKey(keyId, algorithm, publicKey, at, at, TimeSpan.Zero, null, null, null, holdsPrivateKey: true);
    }

    /// <summary>
    /// The key as the store holds it.
    /// </summary>
    /// <param name="keyId">What a token's header names.</param>
    /// <param name="algorithm">What it signs with.</param>
    /// <param name="publicKey">The public key in subject public key information format.</param>
    /// <param name="publishedAt">When it was made.</param>
    /// <param name="signingFrom">When it began signing, or nothing.</param>
    /// <param name="longestLifetime">The longest access-token lifetime it signed under.</param>
    /// <param name="replacedAt">When it was replaced, or nothing.</param>
    /// <param name="overlapEndsAt">When its overlap ends, or nothing.</param>
    /// <param name="keptUntil">When its public key stops being kept, or nothing.</param>
    /// <param name="holdsPrivateKey">Whether the database still holds its private key.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The public key is absent.</exception>
    public static SigningKey Existing(
        string keyId,
        string algorithm,
        byte[] publicKey,
        DateTimeOffset publishedAt,
        DateTimeOffset? signingFrom,
        TimeSpan longestLifetime,
        DateTimeOffset? replacedAt,
        DateTimeOffset? overlapEndsAt,
        DateTimeOffset? keptUntil,
        bool holdsPrivateKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new SigningKey(
            keyId,
            algorithm,
            publicKey,
            publishedAt,
            signingFrom,
            longestLifetime,
            replacedAt,
            overlapEndsAt,
            keptUntil,
            holdsPrivateKey);
    }

    /// <summary>
    /// Whether the key set publishes it: the next key, the current key, and a replaced
    /// key until its overlap ends (AUTH-KEY-001 AC3).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it is published.</returns>
    public bool IsPublished(DateTimeOffset now) =>
        OverlapEndsAt is not DateTimeOffset ends || now < ends;

    /// <summary>
    /// Whether, as the current key, a next key is due: the cadence less the lead has
    /// passed since it began signing.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cadence">How often a key is replaced.</param>
    /// <returns>Whether the next key is to be made.</returns>
    public bool IsNextDue(DateTimeOffset now, TimeSpan cadence) =>
        IsCurrent && SigningFrom is DateTimeOffset from && now >= from + cadence - Lead;

    /// <summary>
    /// Whether, as the next key, it takes over from the current one: the cadence has
    /// passed since the current key began signing, and it has been published for the
    /// lead (AUTH-KEY-001 AC1, AC8).
    /// </summary>
    /// <param name="current">The key signing now.</param>
    /// <param name="now">Now.</param>
    /// <param name="cadence">How often a key is replaced.</param>
    /// <returns>Whether it becomes current.</returns>
    /// <exception cref="ArgumentNullException">The current key is absent.</exception>
    public bool TakesOver(SigningKey current, DateTimeOffset now, TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(current);

        return IsNext
            && current.SigningFrom is DateTimeOffset from
            && now >= from + cadence
            && now >= PublishedAt + Lead;
    }

    /// <summary>
    /// When its overlap ends where it is replaced at the moment given: that moment, the
    /// longest access-token lifetime it signed under, and the margin (AUTH-KEY-001 AC2).
    /// </summary>
    /// <param name="replacedAt">When the next key takes over.</param>
    /// <returns>The end of its overlap.</returns>
    public DateTimeOffset OverlapEnd(DateTimeOffset replacedAt) => replacedAt + LongestLifetime + Margin;

    /// <summary>
    /// Whether its retirement is due: its overlap has ended and its private key is still
    /// held (AUTH-KEY-001 AC5).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it is to be retired.</returns>
    public bool IsRetirementDue(DateTimeOffset now) =>
        HoldsPrivateKey && OverlapEndsAt is DateTimeOffset ends && now >= ends;

    /// <summary>
    /// Whether its removal is due: the longest a session can last has passed since it
    /// was replaced (AUTH-KEY-001 AC6).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it is to be removed.</returns>
    public bool IsRemovalDue(DateTimeOffset now) => KeptUntil is DateTimeOffset kept && now >= kept;
}
