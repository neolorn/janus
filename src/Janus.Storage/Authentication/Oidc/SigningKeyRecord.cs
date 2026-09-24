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

    /// <summary>The <c>private_key</c> column, wrapped under the key-encryption key.</summary>
    [NeverLogged]
    public byte[] PrivateKey { get; set; } = [];

    /// <summary>The <c>key_version</c> column: which key-encryption key wrapped it.</summary>
    public int KeyVersion { get; set; }

    /// <summary>The <c>created_at</c> column: when it began signing.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>superseded_at</c> column, where a newer key took over.</summary>
    public DateTimeOffset? SupersededAt { get; set; }

    /// <summary>The <c>retires_at</c> column: when it leaves the published set.</summary>
    public DateTimeOffset? RetiresAt { get; set; }
}
