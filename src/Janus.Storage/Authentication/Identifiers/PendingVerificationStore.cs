using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Identifiers;

/// <summary>
/// The verifications a live account has outstanding, over the
/// <c>identifier_verifications</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// value being proved and the code sent to prove it are held under the account's own
/// key, so an erasure while a change is in flight leaves neither readable.
/// </remarks>
internal sealed class PendingVerificationStore(
    StoreContext context,
    IKeyRing ring,
    RandomNumberGenerator randomness) : IPendingVerificationStore
{
    /// <inheritdoc/>
    public async ValueTask<PendingVerification?> FindAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        PendingVerificationRecord? record = await context.IdentifierVerifications
            .FindAsync([identifier], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<PendingVerification?> FindForUpdateAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A verification's row is held only inside the operation's transaction.");
        }

        bool tracked = context.IdentifierVerifications.Local.Any(record => record.Identifier == identifier);

        PendingVerificationRecord? held = (await context.IdentifierVerifications
                .FromSql($"SELECT * FROM identity.identifier_verifications WHERE identifier_id = {identifier.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return held is null ? null : await ReadAsync(held, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PendingVerification?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        PendingVerificationRecord? record = await context.IdentifierVerifications
            .FirstOrDefaultAsync(
                pending => pending.Link == fingerprint || pending.OldLink == fingerprint,
                cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PendingVerification>> AddsOfAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<PendingVerificationRecord> records = await context.IdentifierVerifications
            .Where(pending => pending.Subject == subject && !pending.IsReplacement)
            .OrderBy(pending => pending.StagedAt)
            .ThenBy(pending => pending.Identifier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var adds = new List<PendingVerification>(records.Count);

        foreach (PendingVerificationRecord record in records)
        {
            adds.Add(await ReadAsync(record, cancellationToken).ConfigureAwait(false));
        }

        return adds;
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var record = new PendingVerificationRecord
        {
            Identifier = pending.Identifier,
            Subject = pending.Subject,
            Browser = pending.Browser,
            IsReplacement = pending.IsReplacement,
            OldMustConfirm = pending.OldMustConfirm,
            StagedAt = pending.StagedAt,
        };

        await CarryAsync(record, pending, cancellationToken).ConfigureAwait(false);

        await context.IdentifierVerifications.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingVerificationRecord record = await context.IdentifierVerifications
            .FindAsync([pending.Identifier], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The verification has no row to carry the change.");

        await CarryAsync(record, pending, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(IdentifierId identifier, CancellationToken cancellationToken)
    {
        PendingVerificationRecord? record = await context.IdentifierVerifications
            .FindAsync([identifier], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.IdentifierVerifications.Remove(record);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The holders are computed in the statement as
    /// <see cref="PendingVerification.CodeHolder"/> and
    /// <see cref="PendingVerification.ConfirmationHolder"/> compute them: the SHA-256
    /// of the UUID's sixteen bytes in the order of RFC 9562, which is what
    /// <c>uuid_send</c> gives (AUTH-FACT-004). A spent record is a removed one, so a
    /// verification stays exactly while a record under either holder is within its
    /// lifetime.
    /// </remarks>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        byte[] confirmation = PendingVerification.ConfirmationName.ToArray();

        return await context.Database
            .ExecuteSqlAsync(
                $"""
                DELETE FROM identity.identifier_verifications AS pending
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM identity.verification_codes AS held
                    WHERE held.expires_at > {now}
                      AND held.holder IN (
                          sha256(uuid_send(pending.identifier_id)),
                          sha256(uuid_send(pending.identifier_id) || {confirmation})))
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, PendingVerificationConfiguration.Table, PendingVerificationConfiguration.StagedColumn);

    private static StagedIdentityDocument Written(StagedIdentity staged) =>
        new(
            staged.Id.Value,
            VocabularyConverter<IdentifierKind>.Write(staged.Kind),
            staged.Entered,
            staged.Canonical,
            staged.IsLocked,
            staged.IsExtra,
            staged.Link,
            staged.VerifiedAt);

    private static StagedIdentity Read(StagedIdentityDocument staged) =>
        StagedIdentity.Existing(
            new IdentifierId(staged.Id),
            VocabularyConverter<IdentifierKind>.Read(staged.Kind),
            staged.Entered,
            staged.Canonical,
            staged.IsLocked,
            staged.IsExtra,
            staged.Link,
            staged.VerifiedAt);

    private async ValueTask CarryAsync(
        PendingVerificationRecord record,
        PendingVerification pending,
        CancellationToken cancellationToken)
    {
        record.OldConfirmedAt = pending.OldConfirmedAt;
        record.OldLink = pending.OldLink;
        record.Link = pending.Staged.Link;

        byte[] dataKey = await DataKeyAsync(pending.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            record.Staged = PersonalFieldCipher.Encrypt(
                dataKey,
                Located(pending.Subject),
                JsonSerializer.SerializeToUtf8Bytes(
                    Written(pending.Staged),
                    StagedSession.Default.StagedIdentityDocument),
                randomness);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<PendingVerification> ReadAsync(
        PendingVerificationRecord record,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);
        StagedIdentityDocument document;

        try
        {
            document = JsonSerializer.Deserialize(
                PersonalFieldCipher.Decrypt(dataKey, Located(record.Subject), record.Staged),
                StagedSession.Default.StagedIdentityDocument)
                ?? throw new InvalidOperationException("The staged identifier is not a document.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return PendingVerification.Existing(
            record.Subject,
            record.Browser,
            Read(document),
            record.IsReplacement,
            record.OldMustConfirm,
            record.OldConfirmedAt,
            record.OldLink,
            record.StagedAt);
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([SubjectKeyId.Of(subject)], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold a verification under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);
    }
}
