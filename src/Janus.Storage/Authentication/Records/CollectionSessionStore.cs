using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// Collection sessions, over the <c>collection_sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the row's own key is wrapped under.</param>
/// <param name="randomness">The randomness the key and the vectors are drawn from.</param>
/// <remarks>
/// Implements REC-COL-001 and OPS-SEC-001. Everything the steps stage is one encrypted
/// document under a key the row carries, so removing the row removes both the staged
/// contacts and the only key that reads them.
/// </remarks>
internal sealed class CollectionSessionStore(
    StoreContext context,
    DeploymentDataKeyStore deployment,
    RandomNumberGenerator randomness) : ICollectionSessionStore
{
    /// <inheritdoc/>
    public async ValueTask<CollectionSession?> FindAsync(
        CollectionSessionId id,
        CancellationToken cancellationToken)
    {
        CollectionSessionRecord? record = await context.CollectionSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<CollectionSession?> FindForUpdateAsync(
        CollectionSessionId id,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A collection session's row is held only inside the operation's transaction.");
        }

        bool tracked = context.CollectionSessions.Local.Any(record => record.Id == id);

        CollectionSessionRecord? held = (await context.CollectionSessions
                .FromSql($"SELECT * FROM identity.collection_sessions WHERE id = {id.Value} FOR UPDATE")
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
    public async ValueTask AddAsync(CollectionSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var record = new CollectionSessionRecord
            {
                Id = session.Id,
                ExpiresAt = session.ExpiresAt,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, deploymentKey),
                Session = Written(dataKey, session),
            };

            await context.CollectionSessions.AddAsync(record, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(CollectionSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        CollectionSessionRecord record = await context.CollectionSessions
            .FindAsync([session.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The collection session has no row.");

        byte[] dataKey = await DataKeyAsync(record, cancellationToken).ConfigureAwait(false);

        try
        {
            record.Session = Written(dataKey, session);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(CollectionSessionId id, CancellationToken cancellationToken)
    {
        CollectionSessionRecord? record = await context.CollectionSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.CollectionSessions.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.CollectionSessions
            .Where(session => session.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    // PRIV-RIGHT-005a, D-173: the staged values belong to no subject yet, so they are
    // bound to the session's own row.
    private static PersonalFieldLocation Located(CollectionSessionId session) =>
        new(
            new SubjectId(session.Value),
            CollectionSessionConfiguration.Table,
            CollectionSessionConfiguration.SessionColumn);

    private byte[] Written(ReadOnlySpan<byte> dataKey, CollectionSession session)
    {
        var document = new CollectionSessionDocument(
            session.AsksAge,
            session.Language,
            session.CreatedAt,
            session.AdultAffirmed,
            session.Group is AgeGroup group ? VocabularyConverter<AgeGroup>.Write(group) : null,
            session.DateOfBirth,
            session.AnsweredAgeAt,
            session.AgeRefused,
            [
                .. session.Contacts.Select(contact => new CollectedContactDocument(
                    VocabularyConverter<IdentifierKind>.Write(contact.Kind),
                    contact.Value,
                    contact.VerifiedAt)),
            ]);

        return PersonalFieldCipher.Encrypt(
            dataKey,
            Located(session.Id),
            JsonSerializer.SerializeToUtf8Bytes(document, CollectionSessionJson.Default.CollectionSessionDocument),
            randomness);
    }

    private async ValueTask<byte[]> DataKeyAsync(CollectionSessionRecord record, CancellationToken cancellationToken)
    {
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Unwrap(record.WrappedKey, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    private async ValueTask<CollectionSession> ReadAsync(
        CollectionSessionRecord record,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(record, cancellationToken).ConfigureAwait(false);
        CollectionSessionDocument document;

        try
        {
            document = JsonSerializer.Deserialize(
                PersonalFieldCipher.Decrypt(dataKey, Located(record.Id), record.Session),
                CollectionSessionJson.Default.CollectionSessionDocument)
                ?? throw new InvalidOperationException("The staged collection is not a document.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        var session = CollectionSession.Existing(
            record.Id,
            document.AsksAge,
            document.Language,
            document.CreatedAt,
            record.ExpiresAt,
            document.Contacts.Select(contact => new CollectedContact(
                VocabularyConverter<IdentifierKind>.Read(contact.Kind),
                contact.Value,
                contact.VerifiedAt)));

        session.Restore(
            document.AdultAffirmed,
            document.Group is null ? null : VocabularyConverter<AgeGroup>.Read(document.Group),
            document.DateOfBirth,
            document.AnsweredAgeAt,
            document.AgeRefused);

        return session;
    }
}
