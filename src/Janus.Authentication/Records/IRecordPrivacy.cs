using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// What the privacy area does to a person record on staff's word, once the records area
/// has admitted the staff member and judged the record.
/// </summary>
/// <remarks>
/// Implements REC-ADM-002, REC-TKD-001 and CONV-LAYOUT-001. The rows are the records area's and the
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

    /// <summary>
    /// Grants a consent the person gave staff for the record, a record of its own whose
    /// mechanism is administrator, its entry naming the staff member and the reason.
    /// </summary>
    /// <param name="acting">The staff member entering it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>privacy.notice.unpublished</c>, <c>privacy.purpose.noconsent</c>.
    /// </returns>
    ValueTask<Result> GrantConsentAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws a consent of the record on the person's word to staff, its entry naming the
    /// staff member, the mechanism administrator and the reason.
    /// </summary>
    /// <param name="acting">The staff member entering it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>privacy.purpose.noconsent</c>.</returns>
    ValueTask<Result> WithdrawConsentAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an objection the person told staff of for the record, a record of its own whose
    /// mechanism is administrator, its entry naming the staff member and the reason.
    /// </summary>
    /// <param name="acting">The staff member entering it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>privacy.notice.unpublished</c>, <c>privacy.purpose.notobjectable</c>.
    /// </returns>
    ValueTask<Result> ObjectAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws an objection of the record on the person's word to staff, its entry naming
    /// the staff member, the mechanism administrator and the reason.
    /// </summary>
    /// <param name="acting">The staff member entering it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>privacy.purpose.notobjectable</c>.</returns>
    ValueTask<Result> WithdrawObjectionAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string purpose,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assembles the export of a record attached to the asking account, behind the
    /// step-up the account's own export asks, counted against the limit for the record.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The export, or <c>auth.stepup.required</c>, <c>auth.throttled</c>,
    /// <c>system.fault</c>.
    /// </returns>
    ValueTask<Result<SubjectExport>> ExportAttachedAsync(
        AccessContext context,
        SessionId session,
        SubjectId record,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes the record down inside the unit of work the caller opened, the record held
    /// under its lock: the takedown's window begins, every record access session reaching
    /// it ends, and <c>TakedownExecuted</c> is written, with no suspension announced.
    /// </summary>
    /// <param name="context">The staff member taking it down.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="trigger">What raised the report.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The takedown, or nothing where the record is not taken down.</returns>
    ValueTask<ExecutedTakedown?> TakeDownAsync(
        AccessContext context,
        SubjectId record,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The record's latest takedown and how far each subscriber has got with it.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The progress, or nothing where the record holds no takedown.</returns>
    ValueTask<TakedownProgress?> TakedownAsync(SubjectId record, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the record's takedown may be reversed now.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it may, or <c>identity.takedown.notfound</c>,
    /// <c>identity.takedown.windowelapsed</c>.
    /// </returns>
    ValueTask<Error?> ReversalRefusedAsync(SubjectId record, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses the record's takedown inside the unit of work the caller opened,
    /// restoring what it held at the trigger.
    /// </summary>
    /// <param name="context">The staff member reversing it.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing where it was reversed, or the refusal.</returns>
    ValueTask<Error?> ReverseTakedownAsync(
        AccessContext context,
        SubjectId record,
        string reason,
        CancellationToken cancellationToken);
}
