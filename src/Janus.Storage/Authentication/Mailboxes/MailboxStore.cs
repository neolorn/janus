using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Mailboxes;

/// <summary>
/// The mailboxes the library provisions, over the <c>mailboxes</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a data key may be wrapped under.</param>
/// <param name="fingerprintKey">The key the address's fingerprint is computed under.</param>
/// <param name="randomness">The randomness the keys and the vectors are drawn from.</param>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007, PRIV-RIGHT-005a and
/// PRIV-RIGHT-005c. Whether a holder stands is read in the same query as the rows, from
/// the account's state and its memberships of the administrative organization, so the
/// state owed is never a copy that could lag. The address is the holder's personal
/// field, under the holder's key, so erasing the holder leaves it unreadable where it
/// is; while nobody holds the mailbox it is under a key of the row's own. A row whose
/// holder was erased is not read at all.
/// </remarks>
internal sealed class MailboxStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    ReadOnlyMemory<byte> fingerprintKey,
    RandomNumberGenerator randomness) : IMailboxStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MailboxStanding>> AllAsync(CancellationToken cancellationToken)
    {
        var rows = await Readable()
            .Where(mailbox => mailbox.ReleasedAt == null || mailbox.Pushed != MailboxState.Removed)
            .OrderBy(mailbox => mailbox.ReservedAt)
            .Select(mailbox => new
            {
                Row = mailbox,
                Stands = mailbox.Holder != null
                    && context.Accounts.Any(account =>
                        account.Subject == mailbox.Holder && account.State == AccountState.Active)
                    && context.Memberships.Any(membership =>
                        membership.Subject == mailbox.Holder
                        && membership.EndedAt == null
                        && context.Organizations.Any(organization =>
                            organization.Id == membership.Organization && organization.IsAdministrative)),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var standing = new List<MailboxStanding>(rows.Count);

        foreach (var row in rows)
        {
            standing.Add(new MailboxStanding(
                await ReadAsync(row.Row, cancellationToken).ConfigureAwait(false),
                row.Stands));
        }

        return standing;
    }

    /// <inheritdoc/>
    public async ValueTask<Mailbox?> FindAsync(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        byte[] fingerprint = Fingerprinted(address);

        MailboxRecord? record = await Readable()
            .FirstOrDefaultAsync(mailbox => mailbox.Fingerprint == fingerprint, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Mailbox?> FindAsync(MailboxId id, CancellationToken cancellationToken)
    {
        MailboxRecord? record = await Readable()
            .FirstOrDefaultAsync(mailbox => mailbox.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Mailbox?> HeldByAsync(SubjectId holder, CancellationToken cancellationToken)
    {
        // A membership of the administrative organization is held once at most, so an
        // account holds one mailbox at most; a retired one is its last holder's no more.
        MailboxRecord? record = await Readable()
            .SingleOrDefaultAsync(
                mailbox => mailbox.Holder == holder && mailbox.RetiredAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        var record = new MailboxRecord
        {
            Id = mailbox.Id.Value,
            Fingerprint = Fingerprinted(mailbox.Address),
            CanonicalisationVersion = CanonicalForm.UnicodeVersion,
            ReservedAt = mailbox.ReservedAt,
        };

        await CarryAsync(mailbox, record, cancellationToken).ConfigureAwait(false);
        await context.Mailboxes.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        MailboxRecord record = await context.Mailboxes
            .FindAsync([mailbox.Id.Value], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The mailbox has no row to carry the change.");

        await CarryAsync(mailbox, record, cancellationToken).ConfigureAwait(false);
    }

    private static PersonalFieldLocation Located(SubjectId? holder) =>
        new(holder ?? default, MailboxConfiguration.Table, MailboxConfiguration.CanonicalColumn);

    // The address is written again whenever it passes from one key to another: to the
    // holder's when a membership attaches, to a fresh key of the row's own when a
    // retired address is reserved for someone else.
    private async ValueTask CarryAsync(
        Mailbox mailbox,
        MailboxRecord record,
        CancellationToken cancellationToken)
    {
        if (record.EncryptedCanonical.Length is 0 || record.Holder != mailbox.Holder)
        {
            byte[] dataKey = mailbox.Holder is SubjectId holder
                ? await HolderKeyAsync(holder, cancellationToken).ConfigureAwait(false)
                : PersonalFieldCipher.NewDataKey(randomness);

            try
            {
                record.KeyVersion = mailbox.Holder is null ? keyEncryptionKeys.CurrentVersion : null;
                record.WrappedKey = mailbox.Holder is null
                    ? PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span)
                    : null;
                record.EncryptedCanonical = PersonalFieldCipher.Encrypt(
                    dataKey,
                    Located(mailbox.Holder),
                    Encoding.UTF8.GetBytes(mailbox.Address),
                    randomness);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        record.Holder = mailbox.Holder;
        record.RetiredAt = mailbox.RetiredAt;
        record.ReleasedAt = mailbox.ReleasedAt;
        record.Pushed = mailbox.Pushed;
        record.Pending = mailbox.Pending;
        record.PendingKey = mailbox.PendingKey;
        record.Attempts = mailbox.Attempts;
        record.NextAttemptAt = mailbox.NextAttemptAt;
        record.FailedAt = mailbox.FailedAt;
    }

    private async ValueTask<Mailbox> ReadAsync(MailboxRecord record, CancellationToken cancellationToken)
    {
        byte[] dataKey = record.Holder is SubjectId holder
            ? await HolderKeyAsync(holder, cancellationToken).ConfigureAwait(false)
            : PersonalFieldCipher.Unwrap(
                PersonalDataFormat.Marker,
                record.KeyVersion ?? throw new InvalidOperationException("The mailbox has no key."),
                record.WrappedKey ?? throw new InvalidOperationException("The mailbox has no key."),
                keyEncryptionKeys);

        try
        {
            return Mailbox.Existing(
                new MailboxId(record.Id),
                Encoding.UTF8.GetString(
                    PersonalFieldCipher.Decrypt(dataKey, Located(record.Holder), record.EncryptedCanonical)),
                record.ReservedAt,
                record.Holder,
                record.RetiredAt,
                record.ReleasedAt,
                record.Pushed,
                record.Pending,
                record.PendingKey,
                record.Attempts,
                record.NextAttemptAt,
                record.FailedAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<byte[]> HolderKeyAsync(SubjectId holder, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([holder], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The holder has no key to read the mailbox under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }

    // A row whose holder was erased has nothing left that reads its address.
    private IQueryable<MailboxRecord> Readable() =>
        context.Mailboxes
            .Where(mailbox => mailbox.Holder == null
                || context.SubjectKeys.Any(key =>
                    key.Subject == mailbox.Holder && key.FormatMarker == PersonalDataFormat.Marker));

    private byte[] Fingerprinted(string address) =>
        Janus.Storage.Fingerprint.Compute(Encoding.UTF8.GetBytes(address), fingerprintKey.Span);
}
