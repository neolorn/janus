using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Identity.Audit;

/// <summary>
/// The audit trail, over the <c>audit_records</c> table.
/// </summary>
/// <param name="context">The context the subject keys are read through.</param>
/// <param name="connections">Where the append takes its connection from.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
/// <remarks>
/// Implements IDN-AUD-001, IDN-PRIN-001, PRIV-RET-002, PRIV-RET-003 and CONV-DESIGN-003.
/// Nothing here changes or removes a row: the only write is an append. The record is
/// written through the operation's own connection, so an event on a path that opened no
/// transaction stands on its own and one inside a transaction is part of it; a record
/// held for a later save would be lost by an operation that has already committed.
/// </remarks>
internal sealed class AuditStore(
    StoreContext context,
    DataConnections connections,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IAuditStore
{
    private const string Append =
        """
        INSERT INTO identity.audit_records
            (id, category, occurred_at, action, acting_subject, effective_subject,
             organization, details, enc_details, principal, principal_reason)
        VALUES (@id, @category, @at, @action, @acting, @effective, @organization,
                CAST(@details AS jsonb), @personal, @principal, @reason);
        """;

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        byte[]? personal = record.PersonalDetails.Count == 0
            ? null
            : await SealedAsync(record, cancellationToken).ConfigureAwait(false);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Append,
                new
                {
                    id = record.Id.Value,
                    category = VocabularyConverter<AuditCategory>.Write(record.Category),
                    at = record.OccurredAt.ToUniversalTime(),
                    action = record.Action.ToString(),
                    acting = record.ActingSubject.Value,
                    effective = record.EffectiveSubject.Value,
                    organization = record.Organization?.Value,
                    details = Written(record.Details),
                    personal,
                    principal = record.Principal,
                    reason = record.Reason,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AuditRecord>> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<AuditRowRecord> rows = await context.AuditRecords
            .Where(row => row.EffectiveSubject == subject)
            .OrderByDescending(row => row.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = new List<AuditRecord>(rows.Count);
        byte[]? dataKey = null;
        bool sought = false;

        try
        {
            foreach (AuditRowRecord row in rows)
            {
                if (row.PersonalDetails is not null && !sought)
                {
                    sought = true;
                    dataKey = await ReadableKeyAsync(subject, cancellationToken).ConfigureAwait(false);
                }

                records.Add(Read(row, dataKey));
            }
        }
        finally
        {
            if (dataKey is not null)
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        return records;
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, AuditConfiguration.Table, AuditConfiguration.PersonalDetailsColumn);

    private static string Written(IReadOnlyDictionary<string, JsonElement> fields) =>
        JsonSerializer.Serialize(
            new Dictionary<string, JsonElement>(fields, StringComparer.Ordinal),
            AuditDocument.Default.DictionaryStringJsonElement);

    private static Dictionary<string, JsonElement> Fields(ReadOnlySpan<byte> utf8) =>
        JsonSerializer.Deserialize(utf8, AuditDocument.Default.DictionaryStringJsonElement)
            ?? throw new InvalidOperationException("The stored audit fields are not a document.");

    // PRIV-BREACH-002: a record whose subject key is gone comes back anonymised, so
    // the question the trail exists to answer is still answerable after an erasure.
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AuditRecord>> FindNamingAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<AuditRowRecord> rows = await context.AuditRecords
            .Where(row => row.EffectiveSubject == subject || row.ActingSubject == subject)
            .OrderByDescending(row => row.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => Read(row, dataKey: null))];
    }

    private static AuditRecord Read(AuditRowRecord row, byte[]? dataKey) =>
        AuditRecord.Existing(
            row.Id,
            row.Category,
            row.Action,
            row.OccurredAt,
            row.ActingSubject,
            row.EffectiveSubject,
            row.Organization,
            Fields(Encoding.UTF8.GetBytes(row.Details)),
            row.PersonalDetails is null || dataKey is null
                ? new Dictionary<string, JsonElement>(capacity: 0, StringComparer.Ordinal)
                : Fields(PersonalFieldCipher.Decrypt(
                    dataKey,
                    Located(row.EffectiveSubject),
                    row.PersonalDetails)),
            row.Principal,
            row.PrincipalReason);

    private async ValueTask<byte[]> SealedAsync(
        AuditRecord record,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(record.EffectiveSubject, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Encrypt(
                dataKey,
                Located(record.EffectiveSubject),
                Encoding.UTF8.GetBytes(Written(record.PersonalDetails)),
                randomness);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    // The key as a read finds it: absent where the subject has none, and absent where
    // erasure destroyed it (PRIV-RIGHT-005, PRIV-RET-002).
    private async ValueTask<byte[]?> ReadableKeyAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        SubjectKeyRecord? key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        return key is null || key.FormatMarker == PersonalDataFormat.ErasedMarker
            ? null
            : PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold the attribute under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
