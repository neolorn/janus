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
/// The key-encryption key's rotation over the <c>key_rotations</c> table and the subject
/// keys, the only values wrapped under the key.
/// </summary>
/// <param name="context">The context the progress is tracked on.</param>
/// <param name="connections">Where the re-wraps take their connection from.</param>
/// <param name="keyEncryptionKeys">The versions the command was handed, the new one current.</param>
/// <remarks>
/// Implements OPS-SEC-003, OPS-MIG-003a and PRIV-RIGHT-005a. Each key is written back
/// only where it still stands as it was read, version and wrapped value both, so an
/// erasure or a wrapping made meanwhile is never overwritten, and a key is never
/// re-wrapped twice. A value no subject owns is wrapped under the deployment's data key,
/// itself a subject key, so the rotation reaches it through that key and touches no
/// other table.
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
        SELECT DISTINCT key_version FROM identity.subject_keys WHERE format_marker = @marker;
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
        WHERE subject = @subject
            AND format_marker = @marker
            AND key_version = @previous
            AND wrapped_key = @read;
        """;

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
            record.LastKey,
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
                    LastKey = progress.LastKey,
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

        record.LastKey = progress.LastKey;
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
        SubjectKeyId? after,
        int count,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectKey> keys = after is SubjectKeyId last
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

        return new KeyRotationBatch(keys[^1].Id, reWrapped);
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
            .. rows.Select(row => SubjectKey.Existing(new SubjectKeyId(row.Subject), (byte)row.Marker, row.Version, row.Wrapped)),
        ];
    }

    // One subject key unwrapped under the version it stands under and wrapped under the
    // current one, written back only where it still stands as it was read: a key rewritten
    // at the same version meanwhile no longer holds the bytes read, and is left alone.
    private async ValueTask<bool> ReWrappedAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        int previous = key.KeyVersion;
        byte[] read = key.WrappedKey.ToArray();
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
                    subject = key.Id.Value,
                    marker = (short)key.FormatMarker,
                    previous,
                    read,
                    current = key.KeyVersion,
                    wrapped = key.WrappedKey.ToArray(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false) == 1;
    }
}
