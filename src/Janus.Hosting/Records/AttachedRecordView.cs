using System;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A person record attached to the account, as <c>GET /account/records</c> lists it.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization holding it.</param>
/// <param name="OrganizationName">The organization's name.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="AttachedAt">When it was attached.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record AttachedRecordView(
    Guid Subject,
    Guid Organization,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset AttachedAt)
{
    /// <summary>
    /// Shows a record attached to the account.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static AttachedRecordView Of(AttachedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AttachedRecordView(
            record.Subject.Value,
            record.Organization.Value,
            record.OrganizationName,
            record.CreatedAt,
            record.AttachedAt);
    }
}
