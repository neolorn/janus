using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Privacy.Consents;
using Janus.Privacy.Erasures;
using Janus.Privacy.Exports;
using Janus.Privacy.Takedowns;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// What the privacy area does to a person record on staff's word, carried by the
/// privacy area's services.
/// </summary>
/// <param name="exports">Where a record's export is assembled and counted.</param>
/// <param name="consents">Where the consents and objections staff enter for a record are recorded.</param>
/// <param name="erasure">Where a record a host keeps nothing about any longer is erased.</param>
/// <param name="takedowns">Where a record's takedown is carried, read and reversed.</param>
/// <remarks>Implements REC-ADM-002, REC-ADM-003, REC-TKD-001 and CONV-LAYOUT-001.</remarks>
internal sealed class RecordPrivacy(
    ExportService exports,
    ConsentService consents,
    RecordErasure erasure,
    TakedownService takedowns) : IRecordPrivacy
{
    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        CancellationToken cancellationToken) =>
        exports.AssembleOnBehalfAsync(acting, breakGlassReason, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> GrantConsentAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken) =>
        consents.GrantForRecordAsync(acting, breakGlassReason, record, purpose, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawConsentAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken) =>
        consents.WithdrawForRecordAsync(acting, breakGlassReason, record, purpose, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> ObjectAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken) =>
        consents.ObjectForRecordAsync(acting, breakGlassReason, record, purpose, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawObjectionAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken) =>
        consents.WithdrawObjectionForRecordAsync(acting, breakGlassReason, record, purpose, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAttachedAsync(
        AccessContext context,
        SessionId session,
        SubjectId record,
        CancellationToken cancellationToken) =>
        exports.AssembleAttachedAsync(context, session, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask EraseAsync(
        AccessContext context,
        SubjectId record,
        string reason,
        CancellationToken cancellationToken) =>
        erasure.EraseAsync(context, record, reason, cancellationToken);

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
