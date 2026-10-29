using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Privacy.Exports;
using Janus.Privacy.Takedowns;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// What the privacy area does to a person record on staff's word, carried by the
/// privacy area's services.
/// </summary>
/// <param name="exports">Where a record's export is assembled and counted.</param>
/// <param name="takedowns">Where a record's takedown is carried, read and reversed.</param>
/// <remarks>Implements REC-ADM-002, REC-TKD-001 and CONV-LAYOUT-001.</remarks>
internal sealed class RecordPrivacy(ExportService exports, TakedownService takedowns) : IRecordPrivacy
{
    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        CancellationToken cancellationToken) =>
        exports.AssembleOnBehalfAsync(acting, breakGlassReason, record, cancellationToken);
    /// <inheritdoc/>
    public ValueTask<ExecutedTakedown?> TakeDownAsync(
        AccessContext context,
        SubjectId record,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken) =>
        takedowns.TakeDownRecordAsync(context, record, trigger, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TakedownProgress?> TakedownAsync(SubjectId record, CancellationToken cancellationToken) =>
        takedowns.RecordProgressAsync(record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Error?> ReversalRefusedAsync(SubjectId record, CancellationToken cancellationToken) =>
        takedowns.RecordReversalRefusedAsync(record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Error?> ReverseTakedownAsync(
        AccessContext context,
        SubjectId record,
        string reason,
        CancellationToken cancellationToken) =>
        takedowns.ReverseRecordTakedownAsync(context, record, reason, cancellationToken);
}
