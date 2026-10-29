using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The person's rights on one record attached to their account: each the right a record
/// access session exercises, asked in the session of the account the record is attached
/// to.
/// </summary>
/// <param name="attachments">Whether the record is attached to the asking account.</param>
/// <param name="rights">The record's read and its rectification.</param>
/// <param name="consents">The record's consents and objections, as the record's own.</param>
/// <param name="requests">The record's privacy requests, as the record's own.</param>
/// <param name="privacy">Where the record's export is assembled.</param>
/// <remarks>
/// Implements REC-ATT-002, REC-ACC-002 and LIB-API-005. A record not attached to the
/// account is answered as one no record bears; for a right other than the read, the
/// record's state, which is its account's, is judged next. Its deletion is its
/// account's, so none is offered here.
/// </remarks>
internal sealed class AttachedRecordRights(
    RecordAttachments attachments,
    RecordRights rights,
    IConsents consents,
    IPrivacyRequests requests,
    IRecordPrivacy privacy)
{
    /// <summary>
    /// Reads the record.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The record, or the refusal <see cref="IPersonRecords.ReadAttachedAsync"/> names.</returns>
    public async ValueTask<Result<PersonRecordDetail>> ReadAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken)
    {
        if (await RefusedAsync(context, record, admitting: false, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<PersonRecordDetail>(refused);
        }

        return (await rights.ReadAsync(record, cancellationToken).ConfigureAwait(false))
            .Match(
                reading => Result.Success(PersonRecordService.Detail(reading.Record, reading.OrganizationName)),
                Result.Failure<PersonRecordDetail>);
    }

    /// <summary>
    /// Rectifies the record's name and language.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="name">The name, or nothing to leave it.</param>
    /// <param name="language">The language, or nothing to leave it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.RectifyAttachedAsync"/> names.</returns>
    public async ValueTask<Result> RectifyAsync(
        AccessContext context,
        SubjectId record,
        string? name,
        string? language,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : await rights.RectifyAsync(record, name, language, attached: true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Assembles the record's export, behind the step-up the account's own export asks.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The export, or the refusal <see cref="IPersonRecords.ExportAttachedAsync"/> names.</returns>
    public async ValueTask<Result<SubjectExport>> ExportAsync(
        AccessContext context,
        SessionId session,
        SubjectId record,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure<SubjectExport>(refused)
            : await privacy.ExportAttachedAsync(context, session, record, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads the record's consents.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The consents, or the refusal <see cref="IPersonRecords.AttachedConsentsAsync"/> names.</returns>
    public async ValueTask<Result<IReadOnlyList<ConsentRecord>>> ConsentsAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure<IReadOnlyList<ConsentRecord>>(refused)
            : await consents.ReadAsync(AccessContext.Of(record), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Grants a consent for the record, on its dashboard.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.GrantAttachedConsentAsync"/> names.</returns>
    public async ValueTask<Result> GrantAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : await consents
                .GrantAsync(AccessContext.Of(record), purpose, ConsentMechanism.Dashboard, cancellationToken)
                .ConfigureAwait(false);

    /// <summary>
    /// Withdraws a consent of the record.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.WithdrawAttachedConsentAsync"/> names.</returns>
    public async ValueTask<Result> WithdrawAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : await consents.WithdrawAsync(AccessContext.Of(record), purpose, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads the record's objections.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The objections, or the refusal <see cref="IPersonRecords.AttachedObjectionsAsync"/> names.</returns>
    public async ValueTask<Result<IReadOnlyList<ObjectionRecord>>> ObjectionsAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure<IReadOnlyList<ObjectionRecord>>(refused)
            : await consents.ObjectionsAsync(AccessContext.Of(record), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Objects for the record to a purpose, on its dashboard.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.ObjectForAttachedAsync"/> names.</returns>
    public async ValueTask<Result> ObjectAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : await consents
                .ObjectAsync(AccessContext.Of(record), purpose, ConsentMechanism.Dashboard, cancellationToken)
                .ConfigureAwait(false);

    /// <summary>
    /// Withdraws an objection of the record.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.WithdrawAttachedObjectionAsync"/> names.</returns>
    public async ValueTask<Result> WithdrawObjectionAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused
            ? Result.Failure(refused)
            : await consents
                .WithdrawObjectionAsync(AccessContext.Of(record), purpose, cancellationToken)
                .ConfigureAwait(false);

    /// <summary>
    /// Submits a restriction or a rectification request for the record, its receipt
    /// going to the record's notice contacts.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="type">What is requested.</param>
    /// <param name="detail">What the person wrote.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The receipt, or the refusal <see cref="IPersonRecords.SubmitAttachedRequestAsync"/> names.</returns>
    public async ValueTask<Result<PrivacyRequestReceipt>> SubmitAsync(
        AccessContext context,
        SubjectId record,
        PrivacyRequestType type,
        string detail,
        CancellationToken cancellationToken)
    {
        if (await RefusedAsync(context, record, admitting: true, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<PrivacyRequestReceipt>(refused);
        }

        return await requests.SubmitAsync(AccessContext.Of(record), type, detail, cancellationToken).ConfigureAwait(false);
    }

    // Chapter 09 section 7a: the record attached to the account first; then, for a right
    // other than the read, the record's state, which is its account's.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId record,
        bool admitting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (await attachments.ReachesAsync(context, record, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(reached => admitting ? RecordRights.Unadmitted(reached) : null, error => error);
    }
}
