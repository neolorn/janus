using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Privacy.Exports;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// What the privacy area does to a person record on staff's word, carried by the
/// privacy area's services.
/// </summary>
/// <param name="exports">Where a record's export is assembled and counted.</param>
/// <remarks>Implements REC-ADM-002 and CONV-LAYOUT-001.</remarks>
internal sealed class RecordPrivacy(ExportService exports) : IRecordPrivacy
{
    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        CancellationToken cancellationToken) =>
        exports.AssembleOnBehalfAsync(acting, breakGlassReason, record, cancellationToken);
}
