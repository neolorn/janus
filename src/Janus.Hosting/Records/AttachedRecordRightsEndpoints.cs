using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Privacy;
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
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records.ReadAttachedAsync(browser.Asking, subject, cancellationToken).ConfigureAwait(false),
            detail => TypedResults.Json(RecordView.Of(detail), RecordsJson.Default.RecordView));
    }

    private static async Task<IResult> RectifyAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordRectificationRequest request = await JsonBody
            .ReadAsync<RecordRectificationRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await records
                .RectifyAttachedAsync(browser.Asking, subject, request.Name, request.Language, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // REC-ATT-002: PRIV-RIGHT-003's routine for the record, behind the step-up the
    // account's own export asks.
    private static async Task<IResult> ExportAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        string? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        if (PrivacyEndpoints.Arranged(format) is not Func<SubjectExport, IResult> arrangement)
        {
            return Answers.Malformed("format");
        }

        return Answers.Of(
            await records
                .ExportAttachedAsync(browser.Asking, browser.Required.Id, subject, cancellationToken)
                .ConfigureAwait(false),
            arrangement);
    }

    private static async Task<IResult> ConsentsAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records.AttachedConsentsAsync(browser.Asking, subject, cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Held);
    }

    private static async Task<IResult> GrantAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .GrantAttachedConsentAsync(browser.Asking, subject, purpose, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> WithdrawAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .WithdrawAttachedConsentAsync(browser.Asking, subject, purpose, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ObjectionsAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records.AttachedObjectionsAsync(browser.Asking, subject, cancellationToken).ConfigureAwait(false),
            PrivacyEndpoints.Standing);
    }

    private static async Task<IResult> ObjectAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .ObjectForAttachedAsync(browser.Asking, subject, purpose, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> WithdrawObjectionAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .WithdrawAttachedObjectionAsync(browser.Asking, subject, purpose, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // REC-ACC-002: a restriction or a rectification, as the account's own, its receipt
    // going to the record's notice contacts.
    private static async Task<IResult> SubmitAsync(
        IPersonRecords records,
        RequestSession browser,
        SubjectId subject,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

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
            await records
                .SubmitAttachedRequestAsync(browser.Asking, subject, type, detail, cancellationToken)
                .ConfigureAwait(false),
            PrivacyEndpoints.Receipted);
    }
}
