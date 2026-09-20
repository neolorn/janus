using System;
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
    private static readonly IResult Malformed = TypedResults.BadRequest();

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

        _ = group.MapPost("/begin", BeginAsync);
        _ = group.MapPost("/complete", CompleteAsync);
        _ = group.MapPost("/report-loss", ReportLossAsync);
        _ = group.MapPost("/report-loss/{id:guid}/cancel", CancelLossAsync);

        _ = endpoints.MapPost("/admin/recovery/approve", ApproveAsync);
        _ = endpoints.MapPost("/enrol/begin", EnrolAsync);

        return endpoints;
    }

    // AUTH-ABUSE-003: accepted whether or not an account holds the identifier, and
    // the address that holds none is told so.
    private static async Task<IResult> BeginAsync(
        RecoveryRequest request,
        IRecovery recovery,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(context);

        return request.Identifier is not { Length: > 0 } identifier
            ? Malformed
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
        CompleteRecoveryRequest request,
        IRecovery recovery,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(context);

        return request.Token is not { Length: > 0 } token
            || request.Password is not { Length: > 0 } password
            ? Malformed
            : Answers.Of(
                await recovery
                    .CompleteAsync(
                        token,
                        password,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // AUTH-RECOV-007: a session that has presented one usable factor is enough and no
    // step-up is asked for, because the person reporting has lost the factor a gate
    // would ask them for.
    private static async Task<IResult> ReportLossAsync(
        ReportLossRequest request,
        IRecovery recovery,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        if (browser.Context is not AccessContext holder)
        {
            return Nobody();
        }

        return !Guid.TryParse(request.CredentialId, out Guid credential)
            ? Malformed
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
        Guid id,
        CancelLossRequest request,
        IRecovery recovery,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await recovery
                .CancelLossAsync(
                    browser.Context,
                    new AuthenticatorId(id),
                    request.Token,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // AUTH-RECOV-003: the channel is one the account already holds and never one the
    // request supplied; the link goes to that channel and never to this answer.
    private static async Task<IResult> ApproveAsync(
        ApproveRecoveryRequest request,
        IRecovery recovery,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        if (browser.Context is not AccessContext approver || browser.Live is null)
        {
            return Nobody();
        }

        return !Guid.TryParse(request.Subject, out Guid subject)
            || request.ChannelUsed is not { Length: > 0 } channel
            ? Malformed
            : Answers.Of(
                await recovery
                    .ApproveAsync(
                        approver,
                        browser.Live.Id,
                        new SubjectId(subject),
                        request.Reason ?? string.Empty,
                        channel,
                        RequestOrigin.Language(context.Request),
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
        EnrolmentRequest request,
        IRecovery recovery,
        RequestSession browser,
        PreAuthenticationService contacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);

        if (request.Token is not { Length: > 0 } token)
        {
            return Malformed;
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

        return TypedResults.Json(
            EnrolmentSessionView.Of(session),
            RecoveryJson.Default.EnrolmentSessionView,
            contentType: null,
            StatusCodes.Status200OK);
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
