using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>signing_keys</c> row: one key pair the deployment signs tokens with.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The private material is wrapped under the
/// deployment's key-encryption key, whose version is recorded beside it because a
/// wrapped value carries none; the public material is what the key set publishes and
/// is not a secret.
/// </remarks>
internal sealed class SigningKeyRecord
{
    /// <summary>The <c>key_id</c> column: what a token's header names.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>The <c>algorithm</c> column.</summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>The <c>public_key</c> column, in subject public key information format.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>
    /// The <c>private_key</c> column, wrapped under the deployment's data key, and
    /// nothing once the key is retired.
    /// </summary>
    [NeverLogged]
    public byte[]? PrivateKey { get; set; }

    /// <summary>The <c>created_at</c> column: when it was made, and so first published.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>signing_from</c> column: when it began signing, and nothing while it is next.</summary>
    public DateTimeOffset? SigningFrom { get; set; }

    /// <summary>
    /// The <c>longest_lifetime</c> column: the longest access-token lifetime under which
    /// it has signed an access token.
    /// </summary>
    public TimeSpan LongestLifetime { get; set; }

    /// <summary>The <c>superseded_at</c> column, where a newer key took over.</summary>
    public DateTimeOffset? SupersededAt { get; set; }

    /// <summary>The <c>retires_at</c> column: when its overlap ends.</summary>
    public DateTimeOffset? RetiresAt { get; set; }

    /// <summary>The <c>kept_until</c> column: when its public key stops being kept.</summary>
    public DateTimeOffset? KeptUntil { get; set; }

    /// <summary>The <c>is_next</c> column, which the database computes from the times.</summary>
    public bool IsNext { get; set; }

    /// <summary>The <c>is_current</c> column, which the database computes from the times.</summary>
    public bool IsCurrent { get; set; }
}
