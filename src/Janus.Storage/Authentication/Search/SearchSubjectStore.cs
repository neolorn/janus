using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// The subjects the staff search indexes, read and decrypted from the accounts, their
/// profiles, verified identifiers and current memberships, and the person records with
/// their contacts.
/// </summary>
/// <param name="context">The context the reads run on.</param>
/// <param name="ring">The key ring each subject's data key is unwrapped under.</param>
/// <remarks>
/// Implements IDN-SRCH-001 to IDN-SRCH-003, PRIV-RIGHT-005a and CONV-DESIGN-003 (D-256).
/// A subject erased, or in a takedown's window, is not indexed. Each data key is
/// unwrapped for its subject's fields alone and cleared at once; what is decrypted is
/// handed to the index, which is the one place it is kept.
/// </remarks>
internal sealed class SearchSubjectStore(StoreContext context, IKeyRing ring) : ISearchSubjects
{
    /// <inheritdoc/>
    public async ValueTask<int> CountAsync(CancellationToken cancellationToken) =>
        await Accounts(null).CountAsync(cancellationToken).ConfigureAwait(false)
        + await Records(null).CountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SearchSubject>> AllAsync(CancellationToken cancellationToken) =>
        await ReadAsync(null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SearchSubject?> FindAsync(SubjectId subject, CancellationToken cancellationToken) =>
        (await ReadAsync([subject], cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The subjects are absent.</exception>
    public async ValueTask<IReadOnlyDictionary<SubjectId, string?>> NamesAsync(
        IReadOnlyList<SubjectId> subjects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        var names = new Dictionary<SubjectId, string?>(subjects.Count);

        if (subjects.Count == 0)
        {
            return names;
        }

        List<SubjectId> accounts = await Accounts(subjects)
            .Select(account => account.Subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<SubjectId, byte[]> profiles = await ProfilesAsync(accounts, cancellationToken).ConfigureAwait(false);
        List<PersonRecordRow> records = await Records(subjects).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<SubjectId, SubjectKeyRecord> keys = await KeysAsync(
                [.. profiles.Keys, .. records.Where(record => record.Name is not null).Select(record => record.Subject)],
                cancellationToken)
            .ConfigureAwait(false);

        foreach (SubjectId account in accounts)
        {
            names[account] = profiles.TryGetValue(account, out byte[]? stored)
                ? Opened(keys, account, (key, subject) => Read(key, subject, ProfileConfiguration.Table, ProfileConfiguration.DisplayNameColumn, stored))
                : null;
        }

        foreach (PersonRecordRow record in records)
        {
            names[record.Subject] = record.Name is byte[] stored
                ? Opened(keys, record.Subject, (key, subject) => Read(key, subject, PersonRecordConfiguration.Table, PersonRecordConfiguration.NameColumn, stored))
                : null;
        }

        return names;
    }

    private static string Read(ReadOnlySpan<byte> dataKey, SubjectId subject, string table, string column, byte[] stored) =>
        Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(dataKey, new PersonalFieldLocation(subject, table, column), stored));

    private static bool Searchable(IdentifierKind kind) => kind is IdentifierKind.Email or IdentifierKind.Phone;

    private async ValueTask<List<SearchSubject>> ReadAsync(
        IReadOnlyCollection<SubjectId>? only,
        CancellationToken cancellationToken)
    {
        List<SubjectId> accounts = await Accounts(only)
            .Select(account => account.Subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<SubjectId, byte[]> profiles = await ProfilesAsync(accounts, cancellationToken).ConfigureAwait(false);

        // IDN-SRCH-001: an account's verified emails and phones are searched, and nothing
        // it has not verified.
        ILookup<SubjectId, (IdentifierKind Kind, byte[] Canonical)> identifiers = (await context.Identifiers
                .Where(identifier => accounts.Contains(identifier.Subject) && identifier.VerifiedAt != null)
                .Select(identifier => new { identifier.Subject, identifier.Kind, identifier.Canonical })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(identifier => Searchable(identifier.Kind))
            .ToLookup(identifier => identifier.Subject, identifier => (identifier.Kind, identifier.Canonical));

        // IDN-SRCH-002: a membership that ended is no current membership.
        ILookup<SubjectId, OrganizationId> memberships = (await context.Memberships
                .Where(membership => accounts.Contains(membership.Subject) && membership.EndedAt == null)
                .Select(membership => new { membership.Subject, membership.Organization })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(membership => membership.Subject, membership => membership.Organization);

        List<PersonRecordRow> records = await Records(only).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<SubjectId> held = [.. records.Select(record => record.Subject)];
        ILookup<SubjectId, RecordContactRow> contacts = (await context.PersonRecordContacts
                .Where(contact => held.Contains(contact.Subject))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(contact => Searchable(contact.Kind))
            .ToLookup(contact => contact.Subject);

        Dictionary<SubjectId, SubjectKeyRecord> keys = await KeysAsync([.. accounts, .. held], cancellationToken)
            .ConfigureAwait(false);
        var read = new List<SearchSubject>(accounts.Count + records.Count);

        foreach (SubjectId account in accounts)
        {
            read.Add(Opened(keys, account, (key, subject) => new SearchSubject(
                subject,
                SubjectKind.Account,
                profiles.TryGetValue(subject, out byte[]? name)
                    ? Read(key, subject, ProfileConfiguration.Table, ProfileConfiguration.DisplayNameColumn, name)
                    : null,
                [.. identifiers[subject].Where(identifier => identifier.Kind is IdentifierKind.Email)
                    .Select(identifier => Read(key, subject, Identity.Identifiers.IdentifierConfiguration.Table, Identity.Identifiers.IdentifierConfiguration.CanonicalColumn, identifier.Canonical))],
                [.. identifiers[subject].Where(identifier => identifier.Kind is IdentifierKind.Phone)
                    .Select(identifier => Read(key, subject, Identity.Identifiers.IdentifierConfiguration.Table, Identity.Identifiers.IdentifierConfiguration.CanonicalColumn, identifier.Canonical))],
                [.. memberships[subject]])));
        }

        foreach (PersonRecordRow record in records)
        {
            read.Add(Opened(keys, record.Subject, (key, subject) => new SearchSubject(
                subject,
                SubjectKind.Record,
                record.Name is byte[] name
                    ? Read(key, subject, PersonRecordConfiguration.Table, PersonRecordConfiguration.NameColumn, name)
                    : null,
                [.. contacts[subject].Where(contact => contact.Kind is IdentifierKind.Email)
                    .Select(contact => Read(key, subject, RecordContactConfiguration.Table, RecordContactConfiguration.ValueColumn, contact.Value))],
                [.. contacts[subject].Where(contact => contact.Kind is IdentifierKind.Phone)
                    .Select(contact => Read(key, subject, RecordContactConfiguration.Table, RecordContactConfiguration.ValueColumn, contact.Value))],
                [record.Organization])));
        }

        return read;
    }

    // IDN-SRCH-003: an erased account, and one in a takedown's window, is not indexed.
    private IQueryable<Identity.Accounts.AccountRecord> Accounts(IReadOnlyCollection<SubjectId>? only) =>
        context.Accounts.Where(account =>
            (only == null || only.Contains(account.Subject))
            && account.State != AccountState.Deleted
            && !(account.State == AccountState.Deleting && account.DeletingBy == DeletionOrigin.Takedown));

    // IDN-SRCH-003: an erased record, and one in a takedown's window, is not indexed.
    private IQueryable<PersonRecordRow> Records(IReadOnlyCollection<SubjectId>? only) =>
        context.PersonRecords.Where(record =>
            (only == null || only.Contains(record.Subject))
            && record.State != PersonRecordState.Deleted
            && !(record.State == PersonRecordState.Deleting && record.DeletingBy == DeletionOrigin.Takedown));

    private async ValueTask<Dictionary<SubjectId, byte[]>> ProfilesAsync(
        List<SubjectId> accounts,
        CancellationToken cancellationToken) =>
        (await context.Profiles
            .Where(profile => accounts.Contains(profile.Subject) && profile.DisplayName != null)
            .Select(profile => new { profile.Subject, profile.DisplayName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .ToDictionary(profile => profile.Subject, profile => profile.DisplayName!);

    private async ValueTask<Dictionary<SubjectId, SubjectKeyRecord>> KeysAsync(
        List<SubjectId> subjects,
        CancellationToken cancellationToken)
    {
        List<SubjectKeyId> ids = [.. subjects.Select(SubjectKeyId.Of)];

        return (await context.SubjectKeys
                .Where(key => ids.Contains(key.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(key => new SubjectId(key.Id.Value));
    }

    // The subject's data key, lent to one reading of its fields and cleared at once; a
    // subject with nothing to decrypt is read without it.
    private T Opened<T>(
        Dictionary<SubjectId, SubjectKeyRecord> keys,
        SubjectId subject,
        Func<byte[], SubjectId, T> reading)
    {
        if (!keys.TryGetValue(subject, out SubjectKeyRecord? key))
        {
            return reading([], subject);
        }

        byte[] dataKey = PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);

        try
        {
            return reading(dataKey, subject);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }
}
