using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A person record as <c>GET /admin/records/{subject}</c> answers it: as
/// <c>GET /records/access/{subject}</c> answers it, with the account it is attached to.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="OrganizationName">The organization's name.</param>
/// <param name="Origin">How the record came to be.</param>
/// <param name="State">The state it is in.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Name">Its name, or nothing where none was given.</param>
/// <param name="Language">Its language.</param>
/// <param name="Contacts">Its contacts.</param>
/// <param name="Age">Its age answer, or nothing where none was given.</param>
/// <param name="Notices">Each notice version its collections presented.</param>
/// <param name="Terms">Each terms version its collections recorded as accepted.</param>
/// <param name="AttachedTo">The account it is attached to, or nothing.</param>
/// <remarks>Implements REC-ADM-001 and chapter 09 section 8a.</remarks>
internal sealed record HeldRecordView(
    Guid Subject,
    Guid Organization,
    string OrganizationName,
    PersonRecordOrigin Origin,
    PersonRecordState State,
    DateTimeOffset CreatedAt,
    string? Name,
    string Language,
    IReadOnlyList<RecordContactView> Contacts,
    RecordAgeView? Age,
    IReadOnlyList<RecordNoticeView> Notices,
    IReadOnlyList<RecordTermsView> Terms,
    Guid? AttachedTo)
{
    /// <summary>
    /// The view of a record as staff read it.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static HeldRecordView Of(PersonRecordDetail record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new HeldRecordView(
            record.Subject.Value,
            record.Organization.Value,
            record.OrganizationName,
            record.Origin,
            record.State,
            record.CreatedAt,
            record.Name,
            record.Language,
            [.. record.Contacts.Select(contact => new RecordContactView(contact.Kind, contact.Value, contact.VerifiedAt))],
            record.Age is PersonRecordAge age ? new RecordAgeView(age.AnsweredAt, age.AdultAffirmed, age.Group) : null,
            [.. record.Notices.Select(notice => new RecordNoticeView(notice.Version, notice.At))],
            [.. record.Terms.Select(terms => new RecordTermsView(terms.Version, terms.At))],
            record.AttachedTo?.Value);
    }
}
