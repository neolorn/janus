using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// Invitations, over the <c>invitations</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a data key may be wrapped under.</param>
/// <param name="randomness">The randomness the key and the vectors are drawn from.</param>
/// <remarks>
/// Implements IDN-LIFE-009a, REG-INV-001, REG-MAIL-001, PRIV-RIGHT-005a and
/// CONV-DESIGN-003. What the invitation binds belongs to nobody who holds an account,
/// so it is under a key of the row's own, and forgetting it clears the document and
/// the key together.
/// </remarks>
internal sealed class InvitationStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IInvitationStore
{
    /// <inheritdoc/>
    public async ValueTask<Invitation?> FindAsync(InvitationId id, CancellationToken cancellationToken)
    {
        InvitationRecord? record = await context.Invitations
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<Invitation?> FindByTokenAsync(byte[] token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        InvitationRecord? record = await context.Invitations
            .FirstOrDefaultAsync(invitation => invitation.Token == token, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<Invitation?> AttachedToAsync(SubjectId invitee, CancellationToken cancellationToken)
    {
        InvitationRecord? record = await context.Invitations
            .Where(invitation => invitation.Invitee == invitee
                && invitation.RevokedAt == null
                && invitation.AcknowledgedAt == null)
            .OrderByDescending(invitation => invitation.AttachedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Invitation>> ReservingAsync(
        MailboxId mailbox,
        CancellationToken cancellationToken)
    {
        List<InvitationRecord> records = await context.Invitations
            .Where(invitation => invitation.Mailbox == mailbox.Value
                && invitation.RevokedAt == null
                && invitation.AcknowledgedAt == null)
            .OrderBy(invitation => invitation.IssuedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var record = new InvitationRecord
        {
            Id = invitation.Id,
            Organization = invitation.Organization,
            Inviter = invitation.Inviter,
            Token = invitation.Token,
            Roles = [.. invitation.Roles.Select(role => role.ToString())],
            Documents = JsonSerializer.Serialize(
                (IReadOnlyList<InvitedDocument>)
                [
                    .. invitation.Documents.Select(document =>
                        new InvitedDocument(document.Document, document.Version)),
                ],
                InvitationJson.Default.IReadOnlyListInvitedDocument),
            Mailbox = invitation.Mailbox?.Value,
            IssuedAt = invitation.IssuedAt,
            ExpiresAt = invitation.ExpiresAt,
        };

        Carry(invitation, record);

        await context.Invitations.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        InvitationRecord record = await context.Invitations
            .FindAsync([invitation.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The invitation has no row to carry the change.");

        Carry(invitation, record);
    }

    private static PersonalFieldLocation Located() =>
        new(default, InvitationConfiguration.Table, InvitationConfiguration.IdentifiersColumn);

    // The identifiers are written once, when the invitation is issued, and only ever
    // forgotten after that.
    private void Carry(Invitation invitation, InvitationRecord record)
    {
        if (invitation.Identifiers is null)
        {
            record.KeyVersion = null;
            record.WrappedKey = null;
            record.EncryptedIdentifiers = null;
        }
        else if (record.EncryptedIdentifiers is null)
        {
            byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

            try
            {
                record.KeyVersion = keyEncryptionKeys.CurrentVersion;
                record.WrappedKey = PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span);
                record.EncryptedIdentifiers = PersonalFieldCipher.Encrypt(
                    dataKey,
                    Located(),
                    JsonSerializer.SerializeToUtf8Bytes(
                        new InvitedIdentifiersDocument(
                            invitation.Identifiers.Email,
                            invitation.Identifiers.Phone,
                            invitation.Identifiers.CorporateEmail),
                        InvitationJson.Default.InvitedIdentifiersDocument),
                    randomness);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        record.Session = invitation.Session;
        record.Invitee = invitation.Invitee;
        record.AttachedAt = invitation.AttachedAt;
        record.AcknowledgedAt = invitation.AcknowledgedAt;
        record.RevokedAt = invitation.RevokedAt;
    }

    private Invitation Read(InvitationRecord record)
    {
        IReadOnlyList<InvitedDocument> documents =
            JsonSerializer.Deserialize(record.Documents, InvitationJson.Default.IReadOnlyListInvitedDocument)
            ?? throw new InvalidOperationException("The invitation's documents are not a list.");

        return Invitation.Existing(
            record.Id,
            record.Organization,
            record.Inviter,
            record.Token,
            Identifiers(record),
            [.. record.Roles.Select(RoleName.Parse)],
            [.. documents.Select(document => new InvitationDocument(document.Document, document.Version))],
            record.Mailbox is Guid mailbox ? new MailboxId(mailbox) : null,
            record.IssuedAt,
            record.ExpiresAt,
            record.Session,
            record.Invitee,
            record.AttachedAt,
            record.AcknowledgedAt,
            record.RevokedAt);
    }

    private InvitedIdentifiers? Identifiers(InvitationRecord record)
    {
        if (record.EncryptedIdentifiers is null)
        {
            return null;
        }

        byte[] dataKey = PersonalFieldCipher.Unwrap(
            PersonalDataFormat.Marker,
            record.KeyVersion ?? throw new InvalidOperationException("The invitation has no key."),
            record.WrappedKey ?? throw new InvalidOperationException("The invitation has no key."),
            keyEncryptionKeys);

        try
        {
            InvitedIdentifiersDocument document = JsonSerializer.Deserialize(
                    PersonalFieldCipher.Decrypt(dataKey, Located(), record.EncryptedIdentifiers),
                    InvitationJson.Default.InvitedIdentifiersDocument)
                ?? throw new InvalidOperationException("The invitation's identifiers are not a document.");

            return new InvitedIdentifiers(document.Email, document.Phone, document.CorporateEmail);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }
}
