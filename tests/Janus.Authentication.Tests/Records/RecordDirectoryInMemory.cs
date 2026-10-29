using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// The person records a collection reads and writes, held in memory as the tables hold
/// them.
/// </summary>
internal sealed class RecordDirectoryInMemory : IRecordDirectory
{
    private readonly Dictionary<SubjectId, HeldRecord> _records = [];

    private readonly List<(IdentifierKind Kind, string Canonical)> _locked = [];

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, what
    /// the directory wrote inside a unit of work that rolled back is put back.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Every record held.
    /// </summary>
    public IReadOnlyCollection<HeldRecord> All => _records.Values;

    /// <summary>
    /// Every value a collection locked, in the order it locked them.
    /// </summary>
    public IReadOnlyList<(IdentifierKind Kind, string Canonical)> Locked => _locked;

    /// <summary>
    /// The record a subject names.
    /// </summary>
    /// <param name="subject">Whose record.</param>
    /// <returns>The record.</returns>
    public HeldRecord Of(SubjectId subject) => _records[subject];

    /// <summary>
    /// Holds a record a test arranges.
    /// </summary>
    /// <param name="record">The record.</param>
    public void Hold(HeldRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _records[record.Subject] = record;
    }

    /// <inheritdoc/>
    public ValueTask LockValuesAsync(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        CancellationToken cancellationToken)
    {
        _locked.AddRange(values);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<CollectableRecord>> CollectableAsync(
        OrganizationId organization,
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<CollectableRecord>>(
        [
            .. _records.Values
                .Where(record => record is { State: PersonRecordState.Active, AttachedTo: null }
                    && record.Organization == organization
                    && record.Contacts.TryGetValue(kind, out CollectedContact? held)
                    && string.Equals(held.Value, canonical, StringComparison.Ordinal))
                .Select(record => new CollectableRecord(record.Subject, [.. record.Contacts.Values], record.Age is not null)),
        ]);

    /// <inheritdoc/>
    public ValueTask WriteAsync(CollectedPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        Enlist();

        HeldRecord record = person.Created
            ? new HeldRecord(person.Subject, person.Organization, person.At)
            : _records[person.Subject];

        record.Language = person.Language;
        record.Name = person.Name ?? record.Name;
        record.Age = person.Age ?? record.Age;

        foreach (CollectedContact contact in person.Contacts)
        {
            record.Contacts[contact.Kind] = contact;
        }

        if (person.Reference is string reference)
        {
            record.References.Add(reference);
        }

        record.Documents.Add((person.NoticeVersion, person.TermsVersion, person.At));
        _records[record.Subject] = record;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask EnterAsync(EnteredPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        Enlist();

        var record = new HeldRecord(person.Subject, person.Organization, person.At)
        {
            Origin = PersonRecordOrigin.Staff,
            Language = person.Language,
            Name = person.Name,
            Age = person.Age,
        };

        foreach (CollectedContact contact in person.Contacts)
        {
            record.Contacts[contact.Kind] = contact;
        }

        if (person.Reference is string reference)
        {
            record.References.Add(reference);
        }

        _records[record.Subject] = record;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> FingerprintsOf(string canonical) => [Encoding.UTF8.GetBytes(canonical)];

    /// <inheritdoc/>
    public ValueTask<bool> ReachableHoldingAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Reaching(kind, fingerprints, verifiedSince: null).Any());

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ReachedRecord>> ReachedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        DateTimeOffset verifiedSince,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ReachedRecord>>([.. Reaching(kind, fingerprints, verifiedSince).Select(Reached)]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ReachedRecord>> ReferencedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        string reference,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ReachedRecord>>(
        [
            .. Reaching(kind, fingerprints, verifiedSince: null)
                .Where(record => record.References.Contains(reference, StringComparer.Ordinal))
                .Select(Reached),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ReachedRecord>> InReachAsync(
        IReadOnlyList<SubjectId> subjects,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        HeldRecord[] reaching = [.. Reaching(kind, fingerprints, verifiedSince: null)];

        return ValueTask.FromResult<IReadOnlyList<ReachedRecord>>(
        [
            .. subjects.SelectMany(subject => reaching.Where(record => record.Subject == subject)).Select(Reached),
        ]);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SubjectId>> HoldingAsync(
        OrganizationId? organization,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<SubjectId>>(
        [
            .. HeldBy(organization)
                .Where(record => record.Contacts.TryGetValue(kind, out CollectedContact? held)
                    && fingerprints.Any(fingerprint => fingerprint.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(held.Value))))
                .Select(record => record.Subject),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SubjectId>> ReferencingAsync(
        OrganizationId? organization,
        string reference,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<SubjectId>>(
        [
            .. HeldBy(organization)
                .Where(record => record.References.Contains(reference, StringComparer.Ordinal))
                .Select(record => record.Subject),
        ]);

    /// <inheritdoc/>
    public ValueTask<ReadRecord?> ReadAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_records.TryGetValue(subject, out HeldRecord? record)
            ? new ReadRecord(
                record.Subject,
                record.Organization,
                record.Origin,
                record.State,
                record.CreatedAt,
                record.Name?.Value,
                record.Language,
                [.. record.Contacts.Values.OrderBy(contact => contact.Kind)],
                record.Age is CollectedAge age ? new RecordAge(age.AdultAffirmed, age.Group, age.AnsweredAt) : null,
                [.. record.Documents.Select(held => new RecordDocument(held.NoticeVersion, held.At))],
                [
                    .. record.Documents
                        .Where(held => held.TermsVersion is not null)
                        .Select(held => new RecordDocument(held.TermsVersion!, held.At)),
                ],
                record.AttachedTo)
            : null);

    /// <inheritdoc/>
    public ValueTask<RecordStanding?> HoldAsync(SubjectId subject, CancellationToken cancellationToken) =>
        StandingAsync(subject, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<RecordStanding?> StandingAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_records.TryGetValue(subject, out HeldRecord? record)
            ? new RecordStanding(
                record.Organization,
                record.State,
                record.DeletingBy,
                record.DeletingSince,
                record.AttachedTo)
            : null);

    /// <inheritdoc/>
    public ValueTask RectifyAsync(
        SubjectId subject,
        DisplayName? name,
        string? language,
        CancellationToken cancellationToken)
    {
        Enlist();

        HeldRecord record = _records[subject];

        record.Name = name ?? record.Name;
        record.Language = language ?? record.Language;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CorrectContactAsync(
        SubjectId subject,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        Enlist();

        _records[subject].Contacts[kind] = new CollectedContact(kind, value, VerifiedAt: null);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin by,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Enlist();

        HeldRecord record = _records[subject];

        record.RestrictionHeld = record.State is PersonRecordState.Restricted;
        record.State = PersonRecordState.Deleting;
        record.DeletingBy = by;
        record.DeletingSince = at;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CancelDeletionAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        Enlist();

        HeldRecord record = _records[subject];

        record.State = record.RestrictionHeld ? PersonRecordState.Restricted : PersonRecordState.Active;
        record.DeletingBy = null;
        record.DeletingSince = null;
        record.RestrictionHeld = false;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<RecordNoticeContacts?> NoticeContactsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (!_records.TryGetValue(subject, out HeldRecord? record))
        {
            return ValueTask.FromResult<RecordNoticeContacts?>(null);
        }

        CollectedContact[] held = [.. record.Contacts.Values.OrderBy(contact => contact.Kind)];
        CollectedContact[] verified = [.. held.Where(contact => contact.IsVerified)];

        return ValueTask.FromResult<RecordNoticeContacts?>(
            new RecordNoticeContacts(record.Language, verified.Length > 0 ? verified : held));
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Attachment>> AttachedToAsync(SubjectId account, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Attachment>>(
        [
            .. _records.Values
                .Where(record => record.AttachedTo == account && record.State is not PersonRecordState.Deleted)
                .OrderBy(record => record.AttachedAt)
                .ThenBy(record => record.Subject.Value)
                .Select(record => new Attachment(
                    record.Subject,
                    record.Organization,
                    record.CreatedAt,
                    record.AttachedAt!.Value,
                    record.State)),
        ]);

    /// <inheritdoc/>
    public ValueTask<OrganizationId?> AttachAsync(
        SubjectId subject,
        SubjectId account,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!_records.TryGetValue(subject, out HeldRecord? record)
            || record is not { State: PersonRecordState.Active, AttachedTo: null })
        {
            return ValueTask.FromResult<OrganizationId?>(null);
        }

        Enlist();

        HeldRecord held = _records[subject];

        held.AttachedTo = account;
        held.AttachedAt = at;

        return ValueTask.FromResult<OrganizationId?>(held.Organization);
    }

    private static ReachedRecord Reached(HeldRecord record) =>
        new(record.Subject, record.Organization, record.CreatedAt, record.State);

    // A deleted record's fingerprints are neutralised, so nothing finds it.
    private IEnumerable<HeldRecord> HeldBy(OrganizationId? organization) =>
        _records.Values
            .Where(record => (organization is null || record.Organization == organization) && record.State is not PersonRecordState.Deleted)
            .OrderBy(record => record.Subject.Value);

    // REC-ACC-001: collected, attached to no account, active, restricted or deleting by
    // its own request, holding the value as a contact verified at a collection.
    private IEnumerable<HeldRecord> Reaching(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        DateTimeOffset? verifiedSince) =>
        _records.Values
            .Where(record => record is { Origin: PersonRecordOrigin.Collection, AttachedTo: null }
                && (record.State is PersonRecordState.Active or PersonRecordState.Restricted
                    || record is { State: PersonRecordState.Deleting, DeletingBy: DeletionOrigin.Self })
                && record.Contacts.TryGetValue(kind, out CollectedContact? held)
                && held.VerifiedAt is DateTimeOffset verified
                && (verifiedSince is null || verified >= verifiedSince)
                && fingerprints.Any(fingerprint => fingerprint.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(held.Value))))
            .OrderBy(record => record.Subject.Value);

    private void Enlist()
    {
        if (Work is null)
        {
            return;
        }

        var held = _records.ToDictionary(record => record.Key, record => record.Value.Copied());

        Work.Undoing(() =>
        {
            _records.Clear();

            foreach (KeyValuePair<SubjectId, HeldRecord> record in held)
            {
                _records[record.Key] = record.Value;
            }
        });
    }
}
