using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Identifiers;

/// <summary>
/// The verifications a live account has outstanding, over the
/// <c>identifier_verifications</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// value being proved and the code sent to prove it are held under the account's own
/// key, so an erasure while a change is in flight leaves neither readable.
/// </remarks>
internal sealed class PendingVerificationStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
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
    public async ValueTask<int> SweepAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
        await context.IdentifierVerifications
            .Where(pending => pending.StagedAt <= before)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

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
            staged.Code,
            staged.CodeExpiresAt,
            staged.Link,
            staged.WrongAttempts,
            staged.CodeSpent,
            staged.VerifiedAt);

    private static StagedIdentity Read(StagedIdentityDocument staged) =>
        StagedIdentity.Existing(
            new IdentifierId(staged.Id),
            VocabularyConverter<IdentifierKind>.Read(staged.Kind),
            staged.Entered,
            staged.Canonical,
            staged.IsLocked,
            staged.IsExtra,
            staged.Code,
            staged.CodeExpiresAt,
            staged.Link,
            staged.WrongAttempts,
            staged.CodeSpent,
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
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold a verification under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
