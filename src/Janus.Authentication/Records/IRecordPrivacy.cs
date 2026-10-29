using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// What the privacy area does to a person record on staff's word, once the records area
/// has admitted the staff member and judged the record.
/// </summary>
/// <remarks>
/// Implements REC-ADM-002 and CONV-LAYOUT-001. The rows are the records area's and the
/// work is the privacy area's, so the rows ask through this contract and never name
/// what answers it.
/// </remarks>
internal interface IRecordPrivacy
{
    /// <summary>
    /// Assembles the record's export for staff to hand to the person, counted against
    /// the limit for the record and recorded with the staff member acting.
    /// </summary>
    /// <param name="acting">The staff member assembling it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The export, or <c>auth.throttled</c>, <c>system.fault</c>.</returns>
    ValueTask<Result<SubjectExport>> ExportAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        CancellationToken cancellationToken);
}
