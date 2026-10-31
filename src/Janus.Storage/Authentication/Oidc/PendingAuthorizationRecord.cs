using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>pending_authorizations</c> row: an authorization request waiting for the
/// sign-in that answers it.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 (D-224, D-226). The row is bound to exactly one of a
/// browser's pre-authentication session and a session record, and holds the request's
/// parameters wrapped under the deployment's data key.
/// </remarks>
internal sealed class PendingAuthorizationRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>preauthentication</c> column: what the cookie of the browser it waits for
    /// fingerprints to, until its sign-in completes.
    /// </summary>
    [NeverLogged]
    public byte[]? PreAuthentication { get; set; }

    /// <summary>
    /// The <c>session</c> column: the session record the browser's sign-in established.
    /// </summary>
    public SessionId? Session { get; set; }

    /// <summary>
    /// The <c>parameters</c> column: the request's validated parameters, wrapped.
    /// </summary>
    [NeverLogged]
    public byte[] Parameters { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
