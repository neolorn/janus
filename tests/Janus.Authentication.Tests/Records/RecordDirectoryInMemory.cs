using System;
using System.Collections.Generic;
using System.Linq;
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
