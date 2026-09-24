using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The key-encryption key's rotation over the <c>key_rotations</c> table and every
/// column that holds a value wrapped under the key.
/// </summary>
/// <param name="context">The context the progress is tracked on.</param>
/// <param name="connections">Where the re-wraps take their connection from.</param>
/// <param name="keyEncryptionKeys">The versions the command was handed, the new one current.</param>
/// <remarks>
/// Implements OPS-SEC-003, OPS-MIG-003a and PRIV-RIGHT-005a. Each value is written back
/// only where it still stands as it was read, so an erasure or a newer wrapping made
/// meanwhile is never overwritten, and a value is never re-wrapped twice. Of the tables
/// beside the subject keys, the maintenance credential reaches the row's key, the
/// version and the wrapped value and nothing else (entry 316 of the decisions pending
/// review).
/// </remarks>
internal sealed class KeyRotationStore(
    StoreContext context,
    DataConnections connections,
    KeyEncryptionKeys keyEncryptionKeys) : IKeyRotationStore
{
    // OPS-MIG-003a: the rights of the maintenance role, and no path to the application's,
    // which a superuser holds as it holds every role's.
    private const string Credential =
        """
        SELECT pg_has_role(current_user, 'identity_maintenance', 'USAGE')
            AND NOT pg_has_role(current_user, 'identity_app', 'MEMBER');
        """;

    private const string Wrapping =
        """
        SELECT key_version FROM identity.subject_keys WHERE format_marker = @marker
        UNION SELECT key_version FROM identity.invitations WHERE key_version IS NOT NULL
        UNION SELECT key_version FROM identity.mailboxes WHERE key_version IS NOT NULL
        UNION SELECT key_version FROM identity.registration_sessions
        UNION SELECT key_version FROM identity.send_outbox
        UNION SELECT key_version FROM identity.signing_keys
        UNION SELECT signon_key_version FROM identity.preauthentication_sessions
            WHERE signon_key_version IS NOT NULL;
        """;

    private const string First =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        ORDER BY subject
        LIMIT @count;
        """;

    private const string After =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        WHERE subject > @after
        ORDER BY subject
        LIMIT @count;
        """;

    private const string Remaining =
        """
        SELECT subject, format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        WHERE format_marker = @marker AND key_version <> @current
        ORDER BY subject
        LIMIT @count;
        """;

    private const string ReWrap =
        """
        UPDATE identity.subject_keys
        SET key_version = @current, wrapped_key = @wrapped
        WHERE subject = @subject AND format_marker = @marker AND key_version = @previous;
        """;

    // The values wrapped under the key beside the subject keys, each by the column that
    // keys its row, the one that holds the version and the one that holds the value.
    private static readonly HeldColumn[] Held =
    [
        new("invitations", "id", "key_version", "wrapped_key"),
        new("mailboxes", "id", "key_version", "wrapped_key"),
        new("registration_sessions", "id", "key_version", "wrapped_key"),
        new("send_outbox", "id", "key_version", "wrapped_key"),
        new("signing_keys", "key_id", "key_version", "private_key"),
        new("preauthentication_sessions", "fingerprint", "signon_key_version", "signon_verifier"),
    ];

    /// <inheritdoc/>
    public async ValueTask<bool> UnderMaintenanceCredentialAsync(CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<bool>(new CommandDefinition(
                Credential,
                transaction: ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<KeyRotationProgress?> LatestAsync(
        KeyRotationKind kind,
        CancellationToken cancellationToken)
    {
        KeyRotationRecord? record = await context.KeyRotations
            .Where(rotation => rotation.Kind == kind)
            .OrderByDescending(rotation => rotation.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : KeyRotationProgress.Existing(
            record.Kind,
            record.Version,
            record.LastSubject,
            record.Processed,
            record.StartedAt,
            record.CompletedAt,
            record.RetiredAt);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await context.KeyRotations
            .AddAsync(
                new KeyRotationRecord
                {
                    Kind = progress.Kind,
                    Version = progress.Version,
                    LastSubject = progress.LastSubject,
                    Processed = progress.Processed,
                    StartedAt = progress.StartedAt,
                    CompletedAt = progress.CompletedAt,
                    RetiredAt = progress.RetiredAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        KeyRotationRecord record = await context.KeyRotations
                .FindAsync([progress.Kind, progress.Version], cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The rotation has no row to carry its progress.");

        record.LastSubject = progress.LastSubject;
        record.Processed = progress.Processed;
        record.CompletedAt = progress.CompletedAt;
        record.RetiredAt = progress.RetiredAt;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<int>> WrappingVersionsAsync(CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<int> versions = await ambient.Connection
            .QueryAsync<int>(new CommandDefinition(
                Wrapping,
                new { marker = (short)PersonalDataFormat.Marker },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return versions.ToHashSet();
    }

    /// <inheritdoc/>
    public async ValueTask<KeyRotationBatch> ReWrapSubjectKeysAfterAsync(
        SubjectId? after,
        int count,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectKey> keys = after is SubjectId last
            ? await SubjectKeysAsync(After, new { after = last.Value, count }, cancellationToken).ConfigureAwait(false)
            : await SubjectKeysAsync(First, new { count }, cancellationToken).ConfigureAwait(false);

        if (keys.Count == 0)
        {
            return new KeyRotationBatch(Last: null, Processed: 0);
        }

        int reWrapped = 0;

        foreach (SubjectKey key in keys)
        {
            if (!key.IsErased
                && key.KeyVersion != keyEncryptionKeys.CurrentVersion
                && await ReWrappedAsync(key, cancellationToken).ConfigureAwait(false))
            {
                reWrapped++;
            }
        }

        return new KeyRotationBatch(keys[^1].Subject, reWrapped);
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReWrapRemainingSubjectKeysAsync(int count, CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectKey> keys = await SubjectKeysAsync(
                Remaining,
                new { marker = (short)PersonalDataFormat.Marker, current = keyEncryptionKeys.CurrentVersion, count },
                cancellationToken)
            .ConfigureAwait(false);

        int reWrapped = 0;

        foreach (SubjectKey key in keys)
        {
            if (await ReWrappedAsync(key, cancellationToken).ConfigureAwait(false))
            {
                reWrapped++;
            }
        }

        return reWrapped;
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReWrapHeldValuesAsync(int count, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);
        int reWrapped = 0;

        foreach (HeldColumn held in Held)
        {
            if (reWrapped == count)
            {
                break;
            }

            IEnumerable<(object Key, int Version, byte[] Wrapped)> values = await ambient.Connection
                .QueryAsync<(object, int, byte[])>(new CommandDefinition(
                    held.Stale,
                    new { current = keyEncryptionKeys.CurrentVersion, count = count - reWrapped },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            foreach ((object key, int version, byte[] wrapped) in values)
            {
                byte[] value = PersonalFieldCipher.Unwrap(PersonalDataFormat.Marker, version, wrapped, keyEncryptionKeys);

                try
                {
                    reWrapped += await ambient.Connection
                        .ExecuteAsync(new CommandDefinition(
                            held.ReWrap,
                            new
                            {
                                key,
                                previous = version,
                                current = keyEncryptionKeys.CurrentVersion,
                                wrapped = PersonalFieldCipher.Wrap(value, keyEncryptionKeys.Current.Span),
                            },
                            ambient.Transaction,
                            cancellationToken: cancellationToken))
                        .ConfigureAwait(false);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(value);
                }
            }
        }

        return reWrapped;
    }

    private async ValueTask<IReadOnlyList<SubjectKey>> SubjectKeysAsync(
        string query,
        object parameters,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<(Guid Subject, short Marker, int Version, byte[] Wrapped)> rows = await ambient.Connection
            .QueryAsync<(Guid, short, int, byte[])>(new CommandDefinition(
                query,
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => SubjectKey.Existing(new SubjectId(row.Subject), (byte)row.Marker, row.Version, row.Wrapped)),
        ];
    }

    // One subject key unwrapped under the version it stands under and wrapped under the
    // current one, written back only where it still stands as it was read.
    private async ValueTask<bool> ReWrappedAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        int previous = key.KeyVersion;
        byte[] dataKey = PersonalFieldCipher.Unwrap(key.FormatMarker, previous, key.WrappedKey.Span, keyEncryptionKeys);

        try
        {
            key.ReWrap(keyEncryptionKeys.CurrentVersion, PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                ReWrap,
                new
                {
                    subject = key.Subject.Value,
                    marker = (short)key.FormatMarker,
                    previous,
                    current = key.KeyVersion,
                    wrapped = key.WrappedKey.ToArray(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false) == 1;
    }

    // A column holding a value wrapped under the key, with the two statements the
    // rotation runs over it. The names are this class's own constants.
    private sealed record HeldColumn(string Table, string Key, string Version, string Wrapped)
    {
        public string Stale { get; } =
            $"SELECT {Key}, {Version}, {Wrapped} FROM identity.{Table} "
            + $"WHERE {Version} IS NOT NULL AND {Version} <> @current ORDER BY {Key} LIMIT @count;";

        public string ReWrap { get; } =
            $"UPDATE identity.{Table} SET {Version} = @current, {Wrapped} = @wrapped "
            + $"WHERE {Key} = @key AND {Version} = @previous;";
    }
}
