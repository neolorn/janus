using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_codes</c> row: one authorization code waiting to be exchanged.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012. The row holds what the code hashes to and never the code.
/// A spent row stays until the sweep takes it, because a second presentation has to
/// find something to be refused by.
/// </remarks>
internal sealed class AuthorizationCodeRecord
{
    /// <summary>The <c>fingerprint</c> column: what the code hashes to.</summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>The <c>client_id</c> column: which client holds it.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The <c>subject</c> column: whose account it stands for.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>session</c> column: the record the token will be minted from.</summary>
    public SessionId Session { get; set; }

    /// <summary>The <c>redirect</c> column: the destination it was issued against.</summary>
    public string Redirect { get; set; } = string.Empty;

    /// <summary>The <c>challenge</c> column: what the verifier is judged against.</summary>
    public string Challenge { get; set; } = string.Empty;

    /// <summary>The <c>challenge_method</c> column.</summary>
    public string ChallengeMethod { get; set; } = string.Empty;

    /// <summary>The <c>scope</c> column: what the exchange covers.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The <c>nonce</c> column, where the request carried one.</summary>
    public string? Nonce { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>spent_at</c> column, where it has been exchanged.</summary>
    public DateTimeOffset? SpentAt { get; set; }
}
