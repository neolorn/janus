using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The <c>invitations</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 and PRIV-RIGHT-005a. What the
/// invitation binds is one encrypted document under a key the row carries, because no
/// query reads inside it and it is forgotten whole: clearing the document and its key
/// leaves nothing that reads it. The link's token is kept only as its fingerprint.
/// </remarks>
internal sealed class InvitationRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public InvitationId Id { get; set; }

    /// <summary>The <c>organization</c> column: into which organization.</summary>
    public OrganizationId Organization { get; set; }

    /// <summary>The <c>inviter</c> column: who issued it.</summary>
    public SubjectId Inviter { get; set; }

    /// <summary>The <c>token</c> column: the fingerprint of the link's token.</summary>
    [NeverLogged]
    public byte[] Token { get; set; } = [];

    /// <summary>
    /// The <c>key_version</c> column: the key-encryption key version the data key is
    /// wrapped under, while the identifiers are kept.
    /// </summary>
    public int? KeyVersion { get; set; }

    /// <summary>The <c>wrapped_key</c> column: the row's data key, wrapped.</summary>
    public byte[]? WrappedKey { get; set; }

    /// <summary>The <c>enc_identifiers</c> column: what the invitation binds.</summary>
    public byte[]? EncryptedIdentifiers { get; set; }

    /// <summary>The <c>roles</c> column: the roles that attach with the membership.</summary>
    public string[] Roles { get; set; } = [];

    /// <summary>The <c>documents</c> column: the documents shown, each with its version.</summary>
    public string Documents { get; set; } = "[]";

    /// <summary>The <c>mailbox</c> column: the corporate mailbox reserved for it.</summary>
    public Guid? Mailbox { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>session</c> column: the registration it attached to.</summary>
    public RegistrationSessionId? Session { get; set; }

    /// <summary>The <c>invitee</c> column: the account it attached to.</summary>
    public SubjectId? Invitee { get; set; }

    /// <summary>The <c>attached_at</c> column.</summary>
    public DateTimeOffset? AttachedAt { get; set; }

    /// <summary>The <c>acknowledged_at</c> column.</summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>The <c>revoked_at</c> column.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
