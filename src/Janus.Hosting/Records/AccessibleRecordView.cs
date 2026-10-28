using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A person record a record access session reaches, as its state shows it.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="OrganizationName">The organization's name.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="State">The state it is in.</param>
/// <remarks>Implements REC-ACC-001, chapter 09 section 7a.</remarks>
internal sealed record AccessibleRecordView(
    Guid Subject,
    Guid Organization,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    PersonRecordState State)
{
    /// <summary>
    /// Shows a record the session reaches.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static AccessibleRecordView Of(AccessibleRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AccessibleRecordView(
            record.Subject.Value,
            record.Organization.Value,
            record.OrganizationName,
            record.CreatedAt,
            record.State);
    }
}
