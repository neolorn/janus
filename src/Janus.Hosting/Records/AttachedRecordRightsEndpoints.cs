using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Privacy;
using Janus.Privacy.Exports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Records;

/// <summary>
/// The person's rights on one record attached to their account, chapter 09 section 7a.
/// </summary>
/// <remarks>
/// Implements REC-ATT-002 and REC-ACC-002. Each right is the one a record access session
/// exercises, asked in the session of the account the record is attached to; a record not
/// attached to it is answered as one no record bears. Its deletion is its account's, so
/// none is mounted here.
/// </remarks>
internal static class AttachedRecordRightsEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAttachedRecordRights(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/account/records/{subject}");

        _ = SessionRequired.On(group.MapGet("/", ReadAsync))
            .Declares(Reaching())
            .Produces<RecordView>();
        _ = SessionRequired.On(group.MapPut("/", RectifyAsync))
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.Restricted, ErrorCodes.ProfileInvalid,
                ErrorCodes.RequestInvalid))
            .Accepts<RecordRectificationRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapGet("/export", ExportAsync))
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.RequestMalformed, ErrorCodes.StepUpRequired,
                ErrorCodes.Throttled, ErrorCodes.SystemFault))
            .Produces<ExportView>()
            .Produces<PortableExportView>();

        _ = SessionRequired.On(group.MapGet("/consents", ConsentsAsync))
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Produces<IReadOnlyList<ConsentView>>();
        _ = SessionRequired.On(group.MapPost("/consents/{purpose}/grant", GrantAsync))
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.NoticeUnpublished, ErrorCodes.PurposeNoConsent))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/consents/{purpose}/withdraw", WithdrawAsync))
            .Declares(Reaching(ErrorCodes.RecordStateConflict, ErrorCodes.PurposeNoConsent))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapGet("/objections", ObjectionsAsync))
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Produces<IReadOnlyList<ObjectionView>>();
        _ = SessionRequired.On(group.MapPost("/objections/{purpose}", ObjectAsync))
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.NoticeUnpublished, ErrorCodes.PurposeNotObjectable))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapDelete("/objections/{purpose}", WithdrawObjectionAsync))
            .Declares(Reaching(ErrorCodes.RecordStateConflict, ErrorCodes.PurposeNotObjectable))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapPost("/requests", SubmitAsync))
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Accepts<PrivacyRequestBody>(MediaTypeNames.Application.Json)
            .Produces<PrivacyReceiptView>(StatusCodes.Status202Accepted);

        return endpoints;
    }

    private static EndpointDeclaration Reaching(params ErrorCode[] codes) =>
        EndpointDeclaration
            .Answering([ErrorCodes.RecordNotFound, .. codes])
            .Binding<SubjectId>("subject");

    private static async Task<IResult> ReadAsync(
        RecordAttachments attachments,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);

        if (await RefusedAsync(attachments, browser, subject, admitting: false, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await rights.ReadAsync(subject, cancellationToken).ConfigureAwait(false),
            reading => TypedResults.Json(RecordView.Of(reading), RecordsJson.Default.RecordView));
    }

    private static async Task<IResult> RectifyAsync(
        RecordAttachments attachments,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        RecordRectificationRequest request = await JsonBody
            .ReadAsync<RecordRectificationRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await rights
                .RectifyAsync(subject, request.Name, request.Language, attached: true, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // REC-ATT-002: PRIV-RIGHT-003's routine for the record, behind the step-up the
    // account's own export asks.
    private static async Task<IResult> ExportAsync(
        RecordAttachments attachments,
        ExportService exports,
        RequestSession browser,
        SubjectId subject,
        string? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exports);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        if (PrivacyEndpoints.Arranged(format) is not Func<SubjectExport, IResult> arrangement)
        {
            return Answers.Malformed("format");
        }

        return Answers.Of(
            await exports
                .AssembleAttachedAsync(browser.Asking, browser.Required.Id, subject, cancellationToken)
                .ConfigureAwait(false),
            arrangement);
    }

    private static async Task<IResult> ConsentsAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.ReadAsync(AccessContext.Of(subject), cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Held);
    }

    private static async Task<IResult> GrantAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents
                .GrantAsync(AccessContext.Of(subject), purpose, ConsentMechanism.Dashboard, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> WithdrawAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.WithdrawAsync(AccessContext.Of(subject), purpose, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ObjectionsAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.ObjectionsAsync(AccessContext.Of(subject), cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Standing);
    }

    private static async Task<IResult> ObjectAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents
                .ObjectAsync(AccessContext.Of(subject), purpose, ConsentMechanism.Dashboard, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> WithdrawObjectionAsync(
        RecordAttachments attachments,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents
                .WithdrawObjectionAsync(AccessContext.Of(subject), purpose, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // REC-ACC-002: a restriction or a rectification, as the account's own, its receipt
    // going to the record's notice contacts.
    private static async Task<IResult> SubmitAsync(
        RecordAttachments attachments,
        IPrivacyRequests requests,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(attachments, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        PrivacyRequestBody body = await JsonBody
            .ReadAsync<PrivacyRequestBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        // 09 section 7a: the record's erasure is its account's, as its deletion is.
        if (PrivacyEndpoints.Asked(body.Type) is not PrivacyRequestType type || type is PrivacyRequestType.Erasure)
        {
            return Answers.Malformed("type");
        }

        // API-CONV-002: a free-text member is 1 to 1024 characters after trimming.
        if (body.Detail?.Trim() is not { Length: > 0 and <= 1024 } detail)
        {
            return Answers.Malformed("detail");
        }

        return Answers.Of(
            await requests
                .SubmitAsync(AccessContext.Of(subject), type, detail, cancellationToken)
                .ConfigureAwait(false),
            PrivacyEndpoints.Receipted);
    }

    // Chapter 09 section 7a: the record attached to the account first; then, for a right
    // other than the read, the record's state, which is its account's.
    private static async ValueTask<Error?> RefusedAsync(
        RecordAttachments attachments,
        RequestSession browser,
        SubjectId subject,
        bool admitting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(browser);

        return (await attachments.ReachesAsync(browser.Asking, subject, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(reached => admitting ? RecordRights.Unadmitted(reached) : null, error => error);
    }
}
