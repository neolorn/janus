using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Identifiers;

/// <summary>
/// The <c>identifier_verifications</c> row: one identifier of a live account waiting
/// to be proved.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007 and OPS-SEC-001. The value and the code
/// are one encrypted document under the account's own key, because nothing queries
/// inside a waiting verification; the two link fingerprints are columns, because the
/// landing route resolves a token to the row without the row being named in the link.
/// </remarks>
internal sealed class PendingVerificationRecord
{
    /// <summary>The <c>identifier_id</c> column, which is this table's key.</summary>
    public IdentifierId Identifier { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>browser</c> column: the session the change was made from, which is the
    /// only one a press on the landing page proves anything in, and nothing where an
    /// enrolment session made it.
    /// </summary>
    public SessionId? Browser { get; set; }

    /// <summary>The <c>is_replacement</c> column.</summary>
    public bool IsReplacement { get; set; }

    /// <summary>The <c>old_must_confirm</c> column.</summary>
    public bool OldMustConfirm { get; set; }

    /// <summary>The <c>old_confirmed_at</c> column.</summary>
    public DateTimeOffset? OldConfirmedAt { get; set; }

    /// <summary>The <c>old_link</c> column.</summary>
    [NeverLogged]
    public byte[]? OldLink { get; set; }

    /// <summary>The <c>link</c> column: what the new value's link fingerprints to.</summary>
    [NeverLogged]
    public byte[]? Link { get; set; }

    /// <summary>The <c>staged_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset StagedAt { get; set; }

    /// <summary>The <c>enc_staged</c> column: the value being proved and its code.</summary>
    public byte[] Staged { get; set; } = [];
}
