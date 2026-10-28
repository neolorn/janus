using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Records;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// Person records, over the <c>person_records</c> table and the three tables beside it.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements REC-REC-001, REC-REC-002, PRIV-RIGHT-005a, PRIV-RIGHT-005c and
/// CONV-DESIGN-003. The translation between the record and its rows lives here and
/// nowhere else. The record's data key is unwrapped once for an operation and cleared
/// before it returns (PRIV-RIGHT-005a AC12).
/// </remarks>
internal sealed class PersonRecordStore(
    StoreContext context,
    IKeyRing ring,
    RandomNumberGenerator randomness) : IPersonRecordStore
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <inheritdoc/>
    public async ValueTask<PersonRecord?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        PersonRecordRow? row = await context.PersonRecords
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : await ExistingAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PersonRecord?> HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        PersonRecordRow? row = await HeldAsync(context, subject, cancellationToken).ConfigureAwait(false);

        return row is null ? null : await ExistingAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>person_records</c> row, read under a lock held until the operation's
    /// transaction ends. A row the context already tracks was read before the lock, so
    /// it is read again: what the operation decides on is the row committed when the
    /// lock was taken.
    /// </summary>
    /// <param name="context">The context the operation's writes are tracked on.</param>
    /// <param name="subject">Whose row.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The row, tracked, or nothing where no record bears the subject.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    internal static async ValueTask<PersonRecordRow?> HeldAsync(
        StoreContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A person record's row is held only inside the operation's transaction.");
        }

        bool tracked = context.PersonRecords.Local.Any(row => row.Subject == subject);

        PersonRecordRow? held = (await context.PersonRecords
                .FromSql($"SELECT * FROM identity.person_records WHERE subject = {subject.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return held;
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(PersonRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var row = new PersonRecordRow
        {
            Subject = record.Subject,
            Organization = record.Organization,
            Origin = record.Origin,
            CreatedAt = record.CreatedAt,
        };

        await CarryAsync(record, row, cancellationToken).ConfigureAwait(false);

        await context.PersonRecords.AddAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(PersonRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        PersonRecordRow row = await context.PersonRecords
            .FindAsync([record.Subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The person record has no row to carry what it holds.");

        await CarryAsync(record, row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PersonRecord>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        List<PersonRecordRow> rows = await context.PersonRecords
            .Where(row =>
                row.State == PersonRecordState.Deleting
                && (row.DeletingSince <= before || row.DeletionHeldSince <= before))
            .OrderBy(row => row.DeletingSince)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = new List<PersonRecord>(rows.Count);

        foreach (PersonRecordRow row in rows)
        {
            records.Add(await ExistingAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return records;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RecordContact>> ContactsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<RecordContactRow> rows = await context.PersonRecordContacts
            .Where(contact => contact.Subject == subject)
            .OrderBy(contact => contact.Kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count is 0 || await IsErasedAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return
            [
                .. rows.Select(contact => new RecordContact(
                    contact.Kind,
                    Read(dataKey, subject, RecordContactConfiguration.Table, RecordContactConfiguration.ValueColumn, contact.Value),
                    contact.VerifiedAt)),
            ];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetContactAsync(
        SubjectId subject,
        RecordContact contact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contact);

        RecordContactRow? row = await context.PersonRecordContacts
            .FindAsync([subject, contact.Kind], cancellationToken)
            .ConfigureAwait(false);

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            if (row is null)
            {
                row = new RecordContactRow { Subject = subject, Kind = contact.Kind };
                await context.PersonRecordContacts.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }

            byte[] canonical = Encoding.UTF8.GetBytes(contact.Value);

            row.Fingerprint = Fingerprint.Compute(canonical, ring);
            row.FingerprintVersion = Fingerprint.CurrentVersion(ring);
            row.CanonicalisationVersion = CanonicalForm.UnicodeVersion;
            row.Value = PersonalFieldCipher.Encrypt(
                dataKey,
                new PersonalFieldLocation(subject, RecordContactConfiguration.Table, RecordContactConfiguration.ValueColumn),
                canonical,
                randomness);
            row.VerifiedAt = contact.VerifiedAt;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveContactAsync(
        SubjectId subject,
        IdentifierKind kind,
        CancellationToken cancellationToken)
    {
        RecordContactRow? row = await context.PersonRecordContacts
            .FindAsync([subject, kind], cancellationToken)
            .ConfigureAwait(false);

        if (row is not null)
        {
            context.PersonRecordContacts.Remove(row);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SubjectId>> HoldingContactAsync(
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken) =>
        HoldingContactAsync(kind, FingerprintsOf(value), cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> FingerprintsOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Fingerprint.Candidates(Encoding.UTF8.GetBytes(value), ring);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> HoldingContactAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);

        return await context.PersonRecordContacts
            .Where(contact => contact.Kind == kind && fingerprints.Contains(contact.Fingerprint))
            .Select(contact => contact.Subject)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddReferenceAsync(
        SubjectId subject,
        string reference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        await context.PersonRecordReferences
            .AddAsync(
                new RecordReferenceRow
                {
                    Id = Guid.CreateVersion7(at),
                    Subject = subject,
                    Fingerprint = Fingerprint.Compute(Encoding.UTF8.GetBytes(reference), ring),
                    FingerprintVersion = Fingerprint.CurrentVersion(ring),
                    AddedAt = at,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> HoldingReferenceAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        IReadOnlyList<byte[]> candidates = Fingerprint.Candidates(Encoding.UTF8.GetBytes(reference), ring);

        return await context.PersonRecordReferences
            .Where(held => candidates.Contains(held.Fingerprint))
            .Select(held => held.Subject)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddDocumentsAsync(
        SubjectId subject,
        RecordDocuments documents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);

        await context.PersonRecordDocuments
            .AddAsync(
                new RecordDocumentsRow
                {
                    Id = Guid.CreateVersion7(documents.RecordedAt),
                    Subject = subject,
                    NoticeVersion = documents.NoticeVersion,
                    TermsVersion = documents.TermsVersion,
                    RecordedAt = documents.RecordedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RecordDocuments>> DocumentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.PersonRecordDocuments
            .Where(documents => documents.Subject == subject)
            .OrderBy(documents => documents.RecordedAt)
            .Select(documents => new RecordDocuments(
                documents.NoticeVersion,
                documents.TermsVersion,
                documents.RecordedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static string Read(
        ReadOnlySpan<byte> dataKey,
        SubjectId subject,
        string table,
        string column,
        byte[] stored) =>
        Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(
            dataKey,
            new PersonalFieldLocation(subject, table, column),
            stored));

    // A value this library wrote is a value this library accepts, so a stored form that
    // no longer parses is a corrupted row and not a field to drop quietly.
    private static DisplayName? Name(ReadOnlySpan<byte> dataKey, SubjectId subject, byte[]? stored)
    {
        if (stored is null)
        {
            return null;
        }

        string held = Read(dataKey, subject, PersonRecordConfiguration.Table, PersonRecordConfiguration.NameColumn, stored);

        return DisplayName.TryParse(held, out DisplayName name)
            ? name
            : throw new InvalidOperationException("The stored name is not a display name.");
    }

    private static DateOnly? Born(ReadOnlySpan<byte> dataKey, SubjectId subject, byte[]? stored)
    {
        if (stored is null)
        {
            return null;
        }

        string held = Read(dataKey, subject, PersonRecordConfiguration.Table, PersonRecordConfiguration.DateOfBirthColumn, stored);

        return DateOnly.TryParseExact(held, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly born)
            ? born
            : throw new InvalidOperationException("The stored date of birth is not a date.");
    }

    // REC-REC-003: a deleted record's key is gone, so its personal fields are answered
    // as absent and its state, organization, origin and instants alone remain.
    private async ValueTask<PersonRecord> ExistingAsync(PersonRecordRow row, CancellationToken cancellationToken)
    {
        DisplayName? name = null;
        DateOnly? born = null;

        if (row.State is not PersonRecordState.Deleted && (row.Name is not null || row.DateOfBirth is not null))
        {
            byte[] dataKey = await DataKeyAsync(row.Subject, cancellationToken).ConfigureAwait(false);

            try
            {
                name = Name(dataKey, row.Subject, row.Name);
                born = Born(dataKey, row.Subject, row.DateOfBirth);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        return PersonRecord.Existing(
            row.Subject,
            row.Organization,
            row.Origin,
            row.CreatedAt,
            row.Language,
            row.State,
            row.RestrictionHeld,
            row.DeletingBy,
            row.DeletingSince,
            row.DeletionHeld,
            row.DeletionHeldSince,
            row.AttachedTo,
            row.AttachedAt,
            name,
            row.AnsweredAgeAt is DateTimeOffset answered
                ? new RecordAgeAnswer(row.AdultAffirmed, row.AgeGroup, answered, born)
                : null);
    }

    // Re-encrypting an unchanged value would draw a new initialisation vector and write a
    // column the record did not change, so the stored form is read back first and only a
    // value that differs is written again.
    private async ValueTask CarryAsync(PersonRecord record, PersonRecordRow row, CancellationToken cancellationToken)
    {
        row.State = record.State;
        row.Language = record.Language;
        row.RestrictionHeld = record.RestrictionHeld;
        row.DeletingBy = record.DeletingBy;
        row.DeletingSince = record.DeletingSince;
        row.DeletionHeld = record.DeletionHeld;
        row.DeletionHeldSince = record.DeletionHeldSince;
        row.AttachedTo = record.AttachedTo;
        row.AttachedAt = record.AttachedAt;
        row.AdultAffirmed = record.Age?.AdultAffirmed;
        row.AgeGroup = record.Age?.Group;
        row.AnsweredAgeAt = record.Age?.AnsweredAt;

        // A deleted record's fields are left as erasure left them, unreadable.
        if (record.State is PersonRecordState.Deleted)
        {
            return;
        }

        string? name = record.Name?.Value;
        string? born = record.Age?.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture);

        if (name is null && born is null)
        {
            row.Name = null;
            row.DateOfBirth = null;
            return;
        }

        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            row.Name = Carry(row.Name, name, record.Subject, PersonRecordConfiguration.NameColumn, dataKey);
            row.DateOfBirth = Carry(row.DateOfBirth, born, record.Subject, PersonRecordConfiguration.DateOfBirthColumn, dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private byte[]? Carry(
        byte[]? stored,
        string? value,
        SubjectId subject,
        string column,
        ReadOnlySpan<byte> dataKey)
    {
        if (value is null)
        {
            return null;
        }

        return stored is not null
            && string.Equals(Read(dataKey, subject, PersonRecordConfiguration.Table, column, stored), value, StringComparison.Ordinal)
            ? stored
            : PersonalFieldCipher.Encrypt(
                dataKey,
                new PersonalFieldLocation(subject, PersonRecordConfiguration.Table, column),
                Encoding.UTF8.GetBytes(value),
                randomness);
    }

    private async ValueTask<bool> IsErasedAsync(SubjectId subject, CancellationToken cancellationToken) =>
        (await context.PersonRecords.FindAsync([subject], cancellationToken).ConfigureAwait(false))?.State
            is PersonRecordState.Deleted;

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([SubjectKeyId.Of(subject)], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The person record has no key to read its fields under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);
    }
}
