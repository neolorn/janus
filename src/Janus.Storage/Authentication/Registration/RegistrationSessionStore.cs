using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// Registration sessions, over the <c>registration_sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">The operation's connection, for the channel.</param>
/// <param name="keyEncryptionKeys">The versions a data key may be wrapped under.</param>
/// <param name="randomness">The randomness the key and the vectors are drawn from.</param>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-002, REG-SESS-003 and OPS-SEC-001. Everything
/// the steps collect is one encrypted document under a key the row carries, so
/// removing the row removes both the staged data and the only key that reads it.
/// </remarks>
internal sealed class RegistrationSessionStore(
    JanusDbContext context,
    DataConnections connections,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IRegistrationSessionStore
{
    /// <inheritdoc/>
    public async ValueTask<RegistrationSession?> FindAsync(
        RegistrationSessionId id,
        CancellationToken cancellationToken)
    {
        RegistrationSessionRecord? record = await context.RegistrationSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<RegistrationSession?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        RegistrationLinkRecord? link = await context.RegistrationLinks
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        return link is null
            ? null
            : await FindAsync(link.Session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(RegistrationSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

        try
        {
            var record = new RegistrationSessionRecord
            {
                Id = session.Id,
                ProvisionalSubject = session.Provisional,
                ExpiresAt = session.ExpiresAt,
                KeyVersion = keyEncryptionKeys.CurrentVersion,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span),
                Session = Written(dataKey, session),
            };

            await context.RegistrationSessions.AddAsync(record, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        Relink(session, []);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RegistrationSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        RegistrationSessionRecord record = await context.RegistrationSessions
            .FindAsync([session.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The registration session has no row.");

        byte[] dataKey = DataKey(record);

        try
        {
            record.Session = Written(dataKey, session);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        List<RegistrationLinkRecord> held = await context.RegistrationLinks
            .Where(link => link.Session == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Relink(session, held);

        await AnnounceAsync(session.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(
        RegistrationSessionId id,
        CancellationToken cancellationToken)
    {
        RegistrationSessionRecord? record = await context.RegistrationSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.RegistrationSessions.Remove(record);

            await AnnounceAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    // REG-SESS-003: what the waiting screen is watching changed, and the stream the
    // browser holds open is on whichever instance it reached.
    private async ValueTask AnnounceAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections
            .UseAsync(cancellationToken)
            .ConfigureAwait(false);

        await RegistrationChannel
            .RaiseAsync(ambient, session, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.RegistrationSessions
            .Where(session => session.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static PersonalFieldLocation Located(SubjectId provisional) =>
        new(
            provisional,
            RegistrationSessionConfiguration.Table,
            RegistrationSessionConfiguration.SessionColumn);

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

    private static StagedCredentialDocument Written(StagedCredential staged) =>
        new(
            staged.Id.Value,
            VocabularyConverter<Factor>.Write(staged.Factor),
            staged.Label.Value,
            staged.Totp?.Secret.ToArray(),
            staged.WebAuthn?.CredentialId.ToArray(),
            staged.WebAuthn?.PublicKey.ToArray(),
            staged.WebAuthn?.Algorithm,
            staged.WebAuthn?.RelyingPartyId,
            staged.WebAuthn?.Counter,
            staged.WebAuthn?.BackupEligible ?? false,
            staged.WebAuthn?.BackupState ?? false);

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

    private static StagedCredential Read(StagedCredentialDocument staged) =>
        new(
            new AuthenticatorId(staged.Id),
            VocabularyConverter<Factor>.Read(staged.Factor),
            Label(staged.Label),
            staged.TotpSecret is null ? null : new TotpMaterial(staged.TotpSecret, ConsumedStep: null),
            staged.CredentialId is null
                ? null
                : new WebAuthnMaterial(
                    staged.CredentialId,
                    staged.PublicKey!,
                    staged.Algorithm!.Value,
                    staged.RelyingPartyId!,
                    staged.Counter,
                    staged.BackupEligible,
                    staged.BackupState));

    // A label this library wrote is a label this library accepts, so a stored value
    // that no longer parses is a corrupted row and not a label to drop quietly.
    private static CredentialLabel Label(string stored) =>
        CredentialLabel.TryParse(stored, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The stored label is not a label.");

    private byte[] Written(ReadOnlySpan<byte> dataKey, RegistrationSession session)
    {
        var document = new StagedSessionDocument(
            session.Client,
            session.Language,
            session.Source,
            session.CreatedAt,
            VocabularyConverter<RegistrationStep>.Write(session.Step),
            session.DateOfBirth,
            session.AdultAffirmed,
            session.Group is AgeGroup group ? VocabularyConverter<AgeGroup>.Write(group) : null,
            session.AnsweredAgeAt,
            session.AgeRefused,
            session.PhoneSkipped,
            session.Password?.Encoded,
            session.PasswordStandsAlone,
            session.RecoveryCodes?.Select(hash => hash.Encoded).ToArray(),
            [.. session.Identifiers.Select(Written)],
            [.. session.Credentials.Select(Written)]);

        return PersonalFieldCipher.Encrypt(
            dataKey,
            Located(session.Provisional),
            JsonSerializer.SerializeToUtf8Bytes(document, StagedSession.Default.StagedSessionDocument),
            randomness);
    }

    private byte[] DataKey(RegistrationSessionRecord record) =>
        PersonalFieldCipher.Unwrap(
            PersonalDataFormat.Marker,
            record.KeyVersion,
            record.WrappedKey,
            keyEncryptionKeys);

    private RegistrationSession Read(RegistrationSessionRecord record)
    {
        byte[] dataKey = DataKey(record);
        StagedSessionDocument document;

        try
        {
            document = JsonSerializer.Deserialize(
                PersonalFieldCipher.Decrypt(dataKey, Located(record.ProvisionalSubject), record.Session),
                StagedSession.Default.StagedSessionDocument)
                ?? throw new InvalidOperationException("The staged registration is not a document.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        var session = RegistrationSession.Existing(
            record.Id,
            record.ProvisionalSubject,
            document.Client,
            document.Language,
            document.Source,
            document.CreatedAt,
            record.ExpiresAt,
            document.Identifiers.Select(Read),
            document.Credentials.Select(Read));

        session.Restore(
            VocabularyConverter<RegistrationStep>.Read(document.Step),
            document.DateOfBirth,
            document.AdultAffirmed,
            document.Group is null ? null : VocabularyConverter<AgeGroup>.Read(document.Group),
            document.AnsweredAgeAt,
            document.AgeRefused,
            document.Password is null ? null : PasswordHash.Parse(document.Password),
            document.PasswordStandsAlone,
            document.PhoneSkipped,
            document.RecoveryCodes?.Select(PasswordHash.Parse).ToArray(),
            termsVersion: null,
            noticeVersion: null);

        return session;
    }

    // The link rows are what the landing route resolves a token through, so they
    // follow exactly what the session still has outstanding.
    private void Relink(RegistrationSession session, List<RegistrationLinkRecord> held)
    {
        byte[][] outstanding =
        [
            .. session.Identifiers
                .Where(staged => staged.Link is not null)
                .Select(staged => staged.Link!),
        ];

        foreach (RegistrationLinkRecord link in held)
        {
            if (!Array.Exists(outstanding, fingerprint => fingerprint.SequenceEqual(link.Fingerprint)))
            {
                context.RegistrationLinks.Remove(link);
            }
        }

        foreach (byte[] fingerprint in outstanding)
        {
            if (!held.Exists(link => link.Fingerprint.SequenceEqual(fingerprint)))
            {
                context.RegistrationLinks.Add(
                    new RegistrationLinkRecord { Fingerprint = fingerprint, Session = session.Id });
            }
        }
    }
}
