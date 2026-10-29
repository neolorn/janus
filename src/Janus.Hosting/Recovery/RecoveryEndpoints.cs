using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Recovery;

/// <summary>
/// The recovery endpoints of chapter 09 section 5, with the enrolment session of
/// section 3.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, LIB-API-005, AUTH-RECOV-002,
/// AUTH-RECOV-003, AUTH-RECOV-005 and AUTH-RECOV-007. The two links are separate
/// tokens with one endpoint each and are never interchangeable (D-147): the
/// self-service link is consumed by <c>/recovery/complete</c> and the admin-assisted
/// link by <c>/enrol/begin</c>, which is also what binds the enrolment to the browser
/// that opened it.
/// </remarks>
internal static class RecoveryEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRecovery(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/recovery");

        _ = group.MapPost("/begin", BeginAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<RecoveryRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/complete", CompleteAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecoveryTokenInvalid, ErrorCodes.RecoveryTokenExpired, ErrorCodes.PasswordTooShort,
                    ErrorCodes.PasswordTooLong, ErrorCodes.PasswordBlocklisted))
            .Accepts<CompleteRecoveryRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/username", UsernameAsync)
            .Declares(EndpointDeclaration
                .Answering(ErrorCodes.RecoveryTokenInvalid, ErrorCodes.RecoveryTokenExpired, ErrorCodes.Throttled))
            .Accepts<UsernameRequest>(MediaTypeNames.Application.Json)
            .Produces<UsernameView>();
        _ = SessionRequired.On(group.MapPost("/report-loss", ReportLossAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.CredentialNotFound, ErrorCodes.LossReportNotPermitted,
                    ErrorCodes.LossReportPending))
            .Accepts<ReportLossRequest>(MediaTypeNames.Application.Json)
            .Produces<LossReportedView>(StatusCodes.Status202Accepted);
        _ = group.MapPost("/report-loss/{id}/cancel", CancelLossAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.CredentialNotFound,
                    ErrorCodes.LossReportNotPermitted,
                    ErrorCodes.LossReportPending)
                .Binding<AuthenticatorId>("id"))
            .Accepts<CancelLossRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(endpoints.MapPost("/admin/recovery/approve", ApproveAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.RecoveryReasonRequired, ErrorCodes.RecoveryChannelNotOnAccount,
                    ErrorCodes.RecoverySelfApproval, ErrorCodes.SmsBalanceFloor, ErrorCodes.Throttled,
                    ErrorCodes.RestrictionExceeded))
            .Accepts<ApproveRecoveryRequest>(MediaTypeNames.Application.Json)
            .Produces<ApprovedRecoveryView>();
        _ = endpoints.MapPost("/enrol/begin", EnrolAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecoveryTokenExpired, ErrorCodes.EnrolmentTokenInvalid))
            .Accepts<EnrolmentRequest>(MediaTypeNames.Application.Json)
            .Produces<EnrolmentSessionView>();
        _ = EnrolmentRoute.On(endpoints.MapGet("/enrol", ReadEnrolmentAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.SessionExpired))
            .Produces<EnrolmentSessionView>();

        return endpoints;
    }

    // AUTH-ABUSE-003: accepted whether or not an account holds the identifier, and
    // the address that holds none is told so.
    private static async Task<IResult> BeginAsync(
        IRecovery recovery,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(context);

        RecoveryRequest request = await JsonBody
            .ReadAsync<RecoveryRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.Identifier is not { Length: > 0 } identifier
            ? Answers.Malformed("identifier")
            : Answers.Of(
                await recovery
                    .BeginAsync(
                        identifier,
                        RequestOrigin.Language(context.Request),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    // AUTH-RECOV-005, D-147: the self-service link reaches the password and nothing
    // else, and no enrolled second factor is removed by it.
    private static async Task<IResult> CompleteAsync(
        IRecovery recovery,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(context);

        CompleteRecoveryRequest request = await JsonBody
            .ReadAsync<CompleteRecoveryRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Token is not { Length: > 0 } token)
        {
            return Answers.Malformed("token");
        }

        if (request.Password is not { Length: > 0 } password)
        {
            return Answers.Malformed("password");
        }

        return Answers.Of(
                await recovery
                    .CompleteAsync(
                        token,
                        password,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // FE-PM-002 (D-224): the reset form's username is read from the link's token and
    // the read spends nothing, so the token stays for the completion.
    private static async Task<IResult> UsernameAsync(
        IRecovery recovery,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(context);

        UsernameRequest request = await JsonBody
            .ReadAsync<UsernameRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.Token is not { Length: > 0 } token
            ? Answers.Malformed("token")
            : Answers.Of(
                await recovery
                    .UsernameAsync(token, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                username => TypedResults.Json(
                    new UsernameView(username),
                    RecoveryJson.Default.UsernameView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // AUTH-RECOV-007: a session that has presented one usable factor is enough and no
    // step-up is asked for, because the person reporting has lost the factor a gate
    // would ask them for.
    private static async Task<IResult> ReportLossAsync(
        IRecovery recovery,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        ReportLossRequest request = await JsonBody
            .ReadAsync<ReportLossRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        AccessContext holder = Asking(browser);

        return !Guid.TryParse(request.CredentialId, out Guid credential)
            ? Answers.Malformed("credentialId")
            : Answers.Of(
                await recovery
                    .ReportLossAsync(
                        holder,
                        new AuthenticatorId(credential),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                reported => TypedResults.Json(
                    LossReportedView.Of(reported),
                    RecoveryJson.Default.LossReportedView,
                    contentType: null,
                    StatusCodes.Status202Accepted));
    }

    // AUTH-RECOV-007, D-141: a session of the account cancels, and so does the link
    // every notice carried, which is what somebody locked out is holding.
    private static async Task<IResult> CancelLossAsync(
        AuthenticatorId id,
        IRecovery recovery,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        CancelLossRequest request = await JsonBody
            .ReadAsync<CancelLossRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await recovery
                .CancelLossAsync(
                    browser.Context,
                    id,
                    request.Token,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // AUTH-RECOV-003: the channel is one the account already holds and never one the
    // request supplied; the link goes to that channel and never to this answer.
    private static async Task<IResult> ApproveAsync(
        IRecovery recovery,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        ApproveRecoveryRequest request = await JsonBody
            .ReadAsync<ApproveRecoveryRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        AccessContext approver = Asking(browser);

        if (!Guid.TryParse(request.Subject, out Guid subject))
        {
            return Answers.Malformed("subject");
        }

        // AUTH-RECOV-002: chapter 10 names the refusal of an approval without a
        // written reason, so an absent or blank one is answered by it rather than as
        // malformed; free text past 1024 characters after trimming is malformed
        // (API-CONV-002, X4).
        if (request.Reason?.Trim() is not { Length: > 0 } reason)
        {
            return Answers.Refused(Error.From(ErrorCodes.RecoveryReasonRequired));
        }

        if (reason.Length > 1024)
        {
            return Answers.Malformed("reason");
        }

        if (request.ChannelUsed?.Trim() is not { Length: > 0 and <= 1024 } channel)
        {
            return Answers.Malformed("channelUsed");
        }

        return Answers.Of(
                await recovery
                    .ApproveAsync(
                        approver,
                        browser.Required.Id,
                        new SubjectId(subject),
                        reason,
                        channel,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                approved => TypedResults.Json(
                    ApprovedRecoveryView.Of(approved),
                    RecoveryJson.Default.ApprovedRecoveryView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // D-147, D-148: the only endpoint that consumes the admin-assisted link. The
    // session it opens is bound to this browser's first contact exactly as a
    // registration is (BFF-CSRF-005b), so nothing but the browser that opened the
    // link reaches the endpoints that session may call.
    private static async Task<IResult> EnrolAsync(
        IRecovery recovery,
        RequestSession browser,
        PreAuthenticationService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(context);

        EnrolmentRequest request = await JsonBody
            .ReadAsync<EnrolmentRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Token is not { Length: > 0 } token)
        {
            return Answers.Malformed("token");
        }

        if (browser.FirstContact is not PreAuthentication contact)
        {
            return Nobody();
        }

        Error? failure = null;

        EnrolmentSession session = (await recovery
                .BeginEnrolmentAsync(token, cancellationToken)
                .ConfigureAwait(false))
            .Match(opened => opened, error => Withheld<EnrolmentSession>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        await contacts
            .CarryAsync(contact, session.Id, session.ExpiresAt, cancellationToken)
            .ConfigureAwait(false);

        // D-224: the opening carries the document GET /enrol answers.
        return Answers.Of(
            await recovery.ReadEnrolmentAsync(session.Id, cancellationToken).ConfigureAwait(false),
            Enrolment);
    }

    // 09 GET /enrol (D-224): the enrolment screen reads its document on the enrolment
    // session the browser carries, and a browser carrying none is asked nothing.
    private static async Task<IResult> ReadEnrolmentAsync(
        IRecovery recovery,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Enrolment is not EnrolmentSessionId opened
            ? Nobody()
            : Answers.Of(
                await recovery.ReadEnrolmentAsync(opened, cancellationToken).ConfigureAwait(false),
                Enrolment);
    }

    private static IResult Enrolment(EnrolmentDocument document) =>
        TypedResults.Json(
            EnrolmentSessionView.Of(document),
            RecoveryJson.Default.EnrolmentSessionView,
            contentType: null,
            StatusCodes.Status200OK);

    // BFF-STEP-001: the endpoints that read this are mounted as ones that need a
    // session, so the stage that requires one has already answered a request that
    // arrived without it.
    private static AccessContext Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Asking;
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // API-CONV-003: nobody is asking, which is what 401 is for and what nothing else
    // is for.
    private static IResult Nobody() => Answers.Refused(ErrorCodes.SessionExpired);
}
