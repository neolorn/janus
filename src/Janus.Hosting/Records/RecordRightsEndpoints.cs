using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Privacy;
using Janus.Privacy.Exports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Records;

/// <summary>
/// The person's rights on one record a record access session reaches, chapter 09
/// section 7a.
/// </summary>
/// <remarks>
/// Implements REC-ACC-002. Each right is the privacy area's own for an account, asked
/// with the record as the acting subject, so the record's actions are recorded and
/// counted as an account's are. Every endpoint refuses a browser holding a sign-in
/// session, then answers a record the session does not reach as one no record bears;
/// while the record's own deletion runs, it answers its read and its cancellation alone.
/// </remarks>
internal static class RecordRightsEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRecordRights(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/records/access/{subject}");

        _ = group.MapGet("/", ReadAsync)
            .Declares(Reaching())
            .Produces<RecordView>();
        _ = group.MapPut("/", RectifyAsync)
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.Restricted, ErrorCodes.ProfileInvalid,
                ErrorCodes.RequestInvalid))
            .Accepts<RecordRectificationRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapGet("/export", ExportAsync)
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.RequestMalformed, ErrorCodes.Throttled,
                ErrorCodes.SystemFault))
            .Produces<ExportView>()
            .Produces<PortableExportView>();

        _ = group.MapGet("/consents", ConsentsAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Produces<IReadOnlyList<ConsentView>>();
        _ = group.MapPost("/consents/{purpose}/grant", GrantAsync)
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.NoticeUnpublished, ErrorCodes.PurposeNoConsent))
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/consents/{purpose}/withdraw", WithdrawAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict, ErrorCodes.PurposeNoConsent))
            .Produces(StatusCodes.Status204NoContent);

        _ = group.MapGet("/objections", ObjectionsAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Produces<IReadOnlyList<ObjectionView>>();
        _ = group.MapPost("/objections/{purpose}", ObjectAsync)
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.NoticeUnpublished, ErrorCodes.PurposeNotObjectable))
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapDelete("/objections/{purpose}", WithdrawObjectionAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict, ErrorCodes.PurposeNotObjectable))
            .Produces(StatusCodes.Status204NoContent);

        _ = group.MapPost("/requests", SubmitAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict))
            .Accepts<PrivacyRequestBody>(MediaTypeNames.Application.Json)
            .Produces<PrivacyReceiptView>(StatusCodes.Status202Accepted);

        _ = group.MapPost("/delete", DeleteAsync)
            .Declares(Reaching(ErrorCodes.RecordStateConflict, ErrorCodes.TakedownActive))
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/delete/cancel", CancelDeletionAsync)
            .Declares(Reaching(
                ErrorCodes.RecordStateConflict, ErrorCodes.TakedownActive, ErrorCodes.DeletionWindowElapsed))
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static EndpointDeclaration Reaching(params ErrorCode[] codes) =>
        EndpointDeclaration
            .Answering([ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.RecordNotFound, .. codes])
            .Binding<SubjectId>("subject");

    private static async Task<IResult> ReadAsync(
        RecordAccessSessions access,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);

        if (await RefusedAsync(access, browser, subject, admitting: false, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await rights.ReadAsync(subject, cancellationToken).ConfigureAwait(false),
            reading => TypedResults.Json(RecordView.Of(reading), RecordsJson.Default.RecordView));
    }

    private static async Task<IResult> RectifyAsync(
        RecordAccessSessions access,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        RecordRectificationRequest request = await JsonBody
            .ReadAsync<RecordRectificationRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await rights
                .RectifyAsync(subject, request.Name, request.Language, attached: false, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // REC-ACC-002: PRIV-RIGHT-003's routine for the record, with no step-up, since the
    // code that reached it is fresh.
    private static async Task<IResult> ExportAsync(
        RecordAccessSessions access,
        ExportService exports,
        RequestSession browser,
        SubjectId subject,
        string? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exports);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        if (PrivacyEndpoints.Arranged(format) is not Func<SubjectExport, IResult> arrangement)
        {
            return Answers.Malformed("format");
        }

        return Answers.Of(
            await exports.AssembleRecordAsync(subject, cancellationToken).ConfigureAwait(false),
            arrangement);
    }

    private static async Task<IResult> ConsentsAsync(
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.ReadAsync(AccessContext.Of(subject), cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Held);
    }

    private static async Task<IResult> GrantAsync(
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
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
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.WithdrawAsync(AccessContext.Of(subject), purpose, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ObjectionsAsync(
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await consents.ObjectionsAsync(AccessContext.Of(subject), cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Standing);
    }

    private static async Task<IResult> ObjectAsync(
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
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
        RecordAccessSessions access,
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
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
        RecordAccessSessions access,
        IPrivacyRequests requests,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        PrivacyRequestBody body = await JsonBody
            .ReadAsync<PrivacyRequestBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        // 09 section 7a: the record's erasure is its own endpoint, as the account's is.
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

    private static async Task<IResult> DeleteAsync(
        RecordAccessSessions access,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(access, browser, subject, admitting: true, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await rights
                .DeleteAsync(subject, RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> CancelDeletionAsync(
        RecordAccessSessions access,
        RecordRights rights,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rights);

        if (await RefusedAsync(access, browser, subject, admitting: false, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Answers.Refused(refused);
        }

        return Answers.Of(
            await rights.CancelDeletionAsync(subject, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    // Chapter 09 section 7a: the signed-in refusal comes first, as at every step of the
    // session; then the reach; then, for a right other than the read and the
    // cancellation, the record's state.
    private static async ValueTask<Error?> RefusedAsync(
        RecordAccessSessions access,
        RequestSession browser,
        SubjectId subject,
        bool admitting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);

        if (browser.Context is not null)
        {
            return Error.From(ErrorCodes.RecordSignedIn);
        }

        return (await access.ReachesAsync(browser.Access, subject, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(reached => admitting ? RecordRights.Unadmitted(reached) : null, error => error);
    }
}
