using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_tokens</c> row: one code, refresh token or access token the server
/// issued, and what it has since become.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003, AUTH-OIDC-004 and AUTH-KEY-003. A redeemed row stays
/// until the sweep takes it, because a second presentation is what a reuse looks like
/// and it has to find something to be caught by.
/// </remarks>
internal sealed class OidcTokenRecord
{
    /// <summary>The <c>id</c> column: which token.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>concurrency_token</c> column: what a write is refused for not holding,
    /// so two uses of one row cannot both succeed.
    /// </summary>
    public Guid ConcurrencyToken { get; set; }

    /// <summary>The <c>application_id</c> column: which client holds it.</summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>The <c>authorization_id</c> column: the grant it was issued under.</summary>
    public Guid? AuthorizationId { get; set; }

    /// <summary>The <c>subject</c> column: whose account it is for.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>status</c> column: what the server last made of it.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The <c>type</c> column: which of the kinds of token it is.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The <c>reference_id</c> column: what the holder presents, where the token is a
    /// handle on this row rather than a value of its own.
    /// </summary>
    public string? ReferenceId { get; set; }

    /// <summary>The <c>payload</c> column, where the row carries the token itself.</summary>
    public string? Payload { get; set; }

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>The <c>expires_at</c> column.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>The <c>redeemed_at</c> column, where it has been used.</summary>
    public DateTimeOffset? RedeemedAt { get; set; }

    /// <summary>The <c>properties</c> column: what a caller attached to it.</summary>
    public string Properties { get; set; } = "{}";
}
