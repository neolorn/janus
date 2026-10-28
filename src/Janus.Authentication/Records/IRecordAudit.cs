using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where what happened to a person record is recorded.
/// </summary>
/// <remarks>
/// Implements REC-COL-002, REC-COL-003, REC-ACC-001 and chapter 10 section 5.
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

    /// <summary>
    /// Records that staff entered a record, the staff member acting and the record its
    /// subject, filed under its organization.
    /// </summary>
    /// <param name="acting">The staff member.</param>
    /// <param name="breakGlassReason">The reason of the break-glass session the entry was made in, or nothing.</param>
    /// <param name="record">The record.</param>
    /// <param name="organization">The record's organization.</param>
    /// <param name="ageAnswered">Whether the record holds the person's answer to the age question.</param>
    /// <param name="at">The instant of the entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask EnteredAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        OrganizationId organization,
        bool ageAnswered,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a record access session reached a record, at its opening or by a
    /// reference, the record acting and its subject, filed under its organization.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="organization">The record's organization.</param>
    /// <param name="session">The session that reached it.</param>
    /// <param name="byReference">Whether a reference reached it.</param>
    /// <param name="at">The instant it was reached.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AccessedAsync(
        SubjectId record,
        OrganizationId organization,
        RecordAccessSessionId session,
        bool byReference,
        DateTimeOffset at,
        CancellationToken cancellationToken);
    /// <summary>
    /// Records one field of a person record changed, by the field and never by its value
    /// (REC-ACC-002, IDN-AUD-001).
    /// </summary>
    /// <param name="acting">Who changed it: the record itself, the account it is attached to, or staff.</param>
    /// <param name="breakGlassReason">The break-glass reason the acting session carries, or nothing.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="field">The field changed.</param>
    /// <param name="reason">The reason staff gave, or nothing where the person changed it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ChangedAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string field,
        string? reason,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
