using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A person record as <c>GET /records/access/{subject}</c> answers it.
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
/// <param name="Notices">The notice versions its collections presented.</param>
/// <param name="Terms">The terms versions its collections recorded.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordView(
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
    IReadOnlyList<RecordTermsView> Terms)
{
    /// <summary>
    /// Shows a record's read.
    /// </summary>
    /// <param name="reading">The read.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The read is absent.</exception>
    public static RecordView Of(RecordReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        ReadRecord record = reading.Record;

        return new RecordView(
            record.Subject.Value,
            record.Organization.Value,
            reading.OrganizationName,
            record.Origin,
            record.State,
            record.CreatedAt,
            record.Name,
            record.Language,
            [.. record.Contacts.Select(RecordContactView.Of)],
            record.Age is RecordAge age ? RecordAgeView.Of(age) : null,
            [.. record.Notices.Select(notice => new RecordNoticeView(notice.Version, notice.At))],
            [.. record.Terms.Select(terms => new RecordTermsView(terms.Version, terms.At))]);
    }

    /// <summary>
    /// Shows a record attached to the asking account.
    /// </summary>
    /// <param name="detail">The record.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static RecordView Of(PersonRecordDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new RecordView(
            detail.Subject.Value,
            detail.Organization.Value,
            detail.OrganizationName,
            detail.Origin,
            detail.State,
            detail.CreatedAt,
            detail.Name,
            detail.Language,
            [.. detail.Contacts.Select(contact => new RecordContactView(contact.Kind, contact.Value, contact.VerifiedAt))],
            detail.Age is PersonRecordAge age ? new RecordAgeView(age.AnsweredAt, age.AdultAffirmed, age.Group) : null,
            [.. detail.Notices.Select(notice => new RecordNoticeView(notice.Version, notice.At))],
            [.. detail.Terms.Select(terms => new RecordTermsView(terms.Version, terms.At))]);
    }
}
