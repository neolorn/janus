using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where what happened to a person record is recorded.
/// </summary>
/// <remarks>
/// Implements REC-COL-002 and chapter 10 section 5.
/// </remarks>
internal interface IRecordAudit
{
    /// <summary>
    /// Records that a collection wrote or reused a record, the record acting and its
    /// subject, filed under its organization.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="organization">The record's organization.</param>
    /// <param name="created">Whether the collection created it.</param>
    /// <param name="at">The instant of the collection.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CollectedAsync(
        SubjectId record,
        OrganizationId organization,
        bool created,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
