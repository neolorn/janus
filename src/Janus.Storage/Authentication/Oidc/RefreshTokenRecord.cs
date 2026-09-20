using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_refresh_tokens</c> row: one refresh token of one family.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003. The row holds what the token hashes to and never the
/// token. A used row stays until the sweep takes it, because a second presentation is
/// what a reuse looks like and it has to find something to be caught by.
/// </remarks>
internal sealed class RefreshTokenRecord
{
    /// <summary>The <c>fingerprint</c> column: what the token hashes to.</summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>The <c>family</c> column: what a reuse revokes.</summary>
    public RefreshFamilyId Family { get; set; }

    /// <summary>The <c>client_id</c> column: which client holds it.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The <c>subject</c> column: whose account it is for.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>session</c> column: the record it is a handle on.</summary>
    public SessionId Session { get; set; }

    /// <summary>The <c>scope</c> column: what it covers.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the session record decides.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>consumed_at</c> column, where it has been used.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
