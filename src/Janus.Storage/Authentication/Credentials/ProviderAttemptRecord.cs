using System;
using Janus.Authentication.Credentials;
using Janus.Core;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The <c>provider_attempts</c> row: a round trip to a social provider a browser has in
/// flight.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008 and BFF-CSRF-005a. The row holds what the
/// state and the nonce fingerprint to and nothing the browser holds, and the proof key
/// wrapped.
/// </remarks>
internal sealed class ProviderAttemptRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>preauthentication</c> column: the pre-authentication session the attempt
    /// is bound to, where the browser is not signed in.
    /// </summary>
    [NeverLogged]
    public byte[]? PreAuthentication { get; set; }

    /// <summary>
    /// The <c>session</c> column: the session the attempt is bound to, where the
    /// browser is signed in.
    /// </summary>
    public SessionId? Session { get; set; }

    /// <summary>The <c>provider</c> column.</summary>
    public Factor Provider { get; set; }

    /// <summary>The <c>intent</c> column.</summary>
    public ProviderIntent Intent { get; set; }

    /// <summary>
    /// The <c>state</c> column: what the value handed to the browser fingerprints to.
    /// </summary>
    public byte[] State { get; set; } = [];

    /// <summary>
    /// The <c>nonce</c> column: what the nonce the identity token must carry
    /// fingerprints to.
    /// </summary>
    public byte[] Nonce { get; set; } = [];

    /// <summary>
    /// The <c>verifier</c> column: the proof key the token request presents, wrapped
    /// under the key-encryption key, where the provider takes one.
    /// </summary>
    [NeverLogged]
    public byte[]? Verifier { get; set; }

    /// <summary>
    /// The <c>key_version</c> column: which version the proof key is wrapped under.
    /// </summary>
    public int? KeyVersion { get; set; }

    /// <summary>
    /// The <c>return_to</c> column: the path on this application the browser is sent
    /// back to.
    /// </summary>
    public string ReturnTo { get; set; } = string.Empty;

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
